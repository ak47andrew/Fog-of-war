using ChessChallenge.Chess;
using System;
using static ChessChallenge.Chess.PrecomputedMoveData;

namespace ChessChallenge.Application.APIHelpers
{

    public class APIMoveGen
    {

        public const int MaxMoves = 218;

        public MoveGenerator.PromotionMode promotionsToGenerate = MoveGenerator.PromotionMode.All;

        // ---- Instance variables ----
        bool isWhiteToMove;
        int friendlyColour;
        int friendlyKingSquare;
        int friendlyIndex;
        int enemyIndex;

        ulong opponentAttackMapNoPawns;
        public ulong opponentAttackMap;
        public ulong opponentPawnAttackMap;
        ulong opponentSlidingAttackMap;

        bool generateNonCapture;
        Board board;
        int currMoveIndex;

        ulong enemyPieces;
        ulong friendlyPieces;
        ulong allPieces;
        ulong emptySquares;
        ulong emptyOrEnemySquares;
        // If only captures should be generated, this will have 1s only in positions of enemy pieces.
        // Otherwise it will have 1s everywhere.
        ulong moveTypeMask;
        bool hasInitializedCurrentPosition;

        public APIMoveGen()
        {
            board = new Board();
        }

        public bool IsInitialized => hasInitializedCurrentPosition;

        // Movegen needs to know when position has changed to allow for some caching optims in api
        public void NotifyPositionChanged()
        {
            hasInitializedCurrentPosition = false;
        }

        // Generates list of legal moves in current position.
        // Quiet moves (non captures) can optionally be excluded. This is used in quiescence search.
        public void GenerateMoves(ref Span<API.Move> moves, Board board, bool includeQuietMoves = true)
        {
            generateNonCapture = includeQuietMoves;

            Init(board);

            GenerateKingMoves(moves);

            GenerateSlidingMoves(moves);
            GenerateKnightMoves(moves);
            GeneratePawnMoves(moves);

            moves = moves.Slice(0, currMoveIndex);
        }

        public void Init(Board board)
        {
            this.board = board;
            currMoveIndex = 0;


            if (hasInitializedCurrentPosition)
            {
                moveTypeMask = generateNonCapture ? ulong.MaxValue : enemyPieces;
                return;
            }

            hasInitializedCurrentPosition = true;

            // Store some info for convenience
            isWhiteToMove = board.MoveColour == PieceHelper.White;
            friendlyColour = board.MoveColour;
            friendlyKingSquare = board.KingSquare[board.MoveColourIndex];
            friendlyIndex = board.MoveColourIndex;
            enemyIndex = 1 - friendlyIndex;

            // Store some bitboards for convenience
            enemyPieces = board.colourBitboards[enemyIndex];
            friendlyPieces = board.colourBitboards[friendlyIndex];
            allPieces = board.allPiecesBitboard;
            emptySquares = ~allPieces;
            emptyOrEnemySquares = emptySquares | enemyPieces;
            moveTypeMask = generateNonCapture ? ulong.MaxValue : enemyPieces;
        }

        API.Move CreateAPIMove(int startSquare, int targetSquare, int flag)
        {
            int movePieceType = PieceHelper.PieceType(board.Square[startSquare]);
            return CreateAPIMove(startSquare, targetSquare, flag, movePieceType);
        }

        API.Move CreateAPIMove(int startSquare, int targetSquare, int flag, int movePieceType)
        {
            int capturePieceType = PieceHelper.PieceType(board.Square[targetSquare]);
            if (flag == Move.EnPassantCaptureFlag)
            {
                capturePieceType = PieceHelper.Pawn;
            }
            API.Move apiMove = new(new Move(startSquare, targetSquare, flag), movePieceType, capturePieceType);
            return apiMove;
        }

        void GenerateKingMoves(Span<API.Move> moves)
        {
            ulong legalMask = ~(opponentAttackMap | friendlyPieces);
            ulong kingMoves = Bits.KingMoves[friendlyKingSquare] & legalMask & moveTypeMask;
            while (kingMoves != 0)
            {
                int targetSquare = BitBoardUtility.PopLSB(ref kingMoves);
                moves[currMoveIndex++] = CreateAPIMove(friendlyKingSquare, targetSquare, 0, PieceHelper.King);
            }

            // Castling
            if (generateNonCapture)
            {
                ulong castleBlockers = opponentAttackMap | board.allPiecesBitboard;
                if (board.currentGameState.HasKingsideCastleRight(board.IsWhiteToMove))
                {
                    ulong castleMask = board.IsWhiteToMove ? Bits.WhiteKingsideMask : Bits.BlackKingsideMask;
                    if ((castleMask & castleBlockers) == 0)
                    {
                        int targetSquare = board.IsWhiteToMove ? BoardHelper.g1 : BoardHelper.g8;
                        moves[currMoveIndex++] = CreateAPIMove(friendlyKingSquare, targetSquare, Move.CastleFlag, PieceHelper.King);
                    }
                }
                if (board.currentGameState.HasQueensideCastleRight(board.IsWhiteToMove))
                {
                    ulong castleMask = board.IsWhiteToMove ? Bits.WhiteQueensideMask2 : Bits.BlackQueensideMask2;
                    ulong castleBlockMask = board.IsWhiteToMove ? Bits.WhiteQueensideMask : Bits.BlackQueensideMask;
                    if ((castleMask & castleBlockers) == 0 && (castleBlockMask & board.allPiecesBitboard) == 0)
                    {
                        int targetSquare = board.IsWhiteToMove ? BoardHelper.c1 : BoardHelper.c8;
                        moves[currMoveIndex++] = CreateAPIMove(friendlyKingSquare, targetSquare, Move.CastleFlag, PieceHelper.King);
                    }
                }
            }
        }

        void GenerateSlidingMoves(Span<API.Move> moves, bool exitEarly = false)
        {
            // Limit movement to empty or enemy squares, and must block check if king is in check.
            ulong moveMask = emptyOrEnemySquares & moveTypeMask;

            ulong othogonalSliders = board.FriendlyOrthogonalSliders;
            ulong diagonalSliders = board.FriendlyDiagonalSliders;

            // Ortho
            while (othogonalSliders != 0)
            {
                int startSquare = BitBoardUtility.PopLSB(ref othogonalSliders);
                ulong moveSquares = Magic.GetRookAttacks(startSquare, allPieces) & moveMask;

                while (moveSquares != 0)
                {
                    int targetSquare = BitBoardUtility.PopLSB(ref moveSquares);
                    moves[currMoveIndex++] = CreateAPIMove(startSquare, targetSquare, 0);
                    if (exitEarly)
                    {
                        return;
                    }
                }
            }

            // Diag
            while (diagonalSliders != 0)
            {
                int startSquare = BitBoardUtility.PopLSB(ref diagonalSliders);
                ulong moveSquares = Magic.GetBishopAttacks(startSquare, allPieces) & moveMask;

                while (moveSquares != 0)
                {
                    int targetSquare = BitBoardUtility.PopLSB(ref moveSquares);
                    moves[currMoveIndex++] = CreateAPIMove(startSquare, targetSquare, 0);
                    if (exitEarly)
                    {
                        return;
                    }
                }
            }
        }


        void GenerateKnightMoves(Span<API.Move> moves)
        {
            int friendlyKnightPiece = PieceHelper.MakePiece(PieceHelper.Knight, board.MoveColour);
            ulong knights = board.pieceBitboards[friendlyKnightPiece];
            ulong moveMask = emptyOrEnemySquares & moveTypeMask;

            while (knights != 0)
            {
                int knightSquare = BitBoardUtility.PopLSB(ref knights);
                ulong moveSquares = Bits.KnightAttacks[knightSquare] & moveMask;

                while (moveSquares != 0)
                {
                    int targetSquare = BitBoardUtility.PopLSB(ref moveSquares);
                    moves[currMoveIndex++] = CreateAPIMove(knightSquare, targetSquare, 0, PieceHelper.Knight);
                }
            }
        }

        void GeneratePawnMoves(Span<API.Move> moves)
        {
            int pushDir = board.IsWhiteToMove ? 1 : -1;
            int pushOffset = pushDir * 8;

            int friendlyPawnPiece = PieceHelper.MakePiece(PieceHelper.Pawn, board.MoveColour);
            ulong pawns = board.pieceBitboards[friendlyPawnPiece];

            ulong promotionRankMask = board.IsWhiteToMove ? Bits.Rank8 : Bits.Rank1;

            ulong singlePush = (BitBoardUtility.Shift(pawns, pushOffset)) & emptySquares;

            ulong pushPromotions = singlePush & promotionRankMask;


            ulong captureEdgeFileMask = board.IsWhiteToMove ? Bits.NotAFile : Bits.NotHFile;
            ulong captureEdgeFileMask2 = board.IsWhiteToMove ? Bits.NotHFile : Bits.NotAFile;
            ulong captureA = BitBoardUtility.Shift(pawns & captureEdgeFileMask, pushDir * 7) & enemyPieces;
            ulong captureB = BitBoardUtility.Shift(pawns & captureEdgeFileMask2, pushDir * 9) & enemyPieces;

            ulong singlePushNoPromotions = singlePush & ~promotionRankMask;

            ulong capturePromotionsA = captureA & promotionRankMask;
            ulong capturePromotionsB = captureB & promotionRankMask;

            captureA &= ~promotionRankMask;
            captureB &= ~promotionRankMask;

            // Single / double push
            if (generateNonCapture)
            {
                // Generate single pawn pushes
                while (singlePushNoPromotions != 0)
                {
                    int targetSquare = BitBoardUtility.PopLSB(ref singlePushNoPromotions);
                    int startSquare = targetSquare - pushOffset;
                    moves[currMoveIndex++] = CreateAPIMove(startSquare, targetSquare, 0, PieceHelper.Pawn);
                }

                // Generate double pawn pushes
                ulong doublePushTargetRankMask = board.IsWhiteToMove ? Bits.Rank4 : Bits.Rank5;
                ulong doublePush = BitBoardUtility.Shift(singlePush, pushOffset) & emptySquares & doublePushTargetRankMask;

                while (doublePush != 0)
                {
                    int targetSquare = BitBoardUtility.PopLSB(ref doublePush);
                    int startSquare = targetSquare - pushOffset * 2;
                    moves[currMoveIndex++] = CreateAPIMove(startSquare, targetSquare, Move.PawnTwoUpFlag, PieceHelper.Pawn);
                }
            }

            // Captures
            while (captureA != 0)
            {
                int targetSquare = BitBoardUtility.PopLSB(ref captureA);
                int startSquare = targetSquare - pushDir * 7;
                moves[currMoveIndex++] = CreateAPIMove(startSquare, targetSquare, 0, PieceHelper.Pawn);
            }

            while (captureB != 0)
            {
                int targetSquare = BitBoardUtility.PopLSB(ref captureB);
                int startSquare = targetSquare - pushDir * 9;
                moves[currMoveIndex++] = CreateAPIMove(startSquare, targetSquare, 0, PieceHelper.Pawn);
            }



            // Promotions
            if (generateNonCapture)
            {
                while (pushPromotions != 0)
                {
                    int targetSquare = BitBoardUtility.PopLSB(ref pushPromotions);
                    int startSquare = targetSquare - pushOffset;
                    GeneratePromotions(moves, startSquare, targetSquare);
                }
            }


            while (capturePromotionsA != 0)
            {
                int targetSquare = BitBoardUtility.PopLSB(ref capturePromotionsA);
                int startSquare = targetSquare - pushDir * 7;
                GeneratePromotions(moves, startSquare, targetSquare);
            }

            while (capturePromotionsB != 0)
            {
                int targetSquare = BitBoardUtility.PopLSB(ref capturePromotionsB);
                int startSquare = targetSquare - pushDir * 9;
                GeneratePromotions(moves, startSquare, targetSquare);
            }

            // En passant
            if (board.currentGameState.enPassantFile > 0)
            {
                int epFileIndex = board.currentGameState.enPassantFile - 1;
                int epRankIndex = board.IsWhiteToMove ? 5 : 2;
                int targetSquare = epRankIndex * 8 + epFileIndex;
                int capturedPawnSquare = targetSquare - pushOffset;

                ulong pawnsThatCanCaptureEp = pawns & BitBoardUtility.PawnAttacks(1ul << targetSquare, !board.IsWhiteToMove);

                while (pawnsThatCanCaptureEp != 0)
                {
                    int startSquare = BitBoardUtility.PopLSB(ref pawnsThatCanCaptureEp);
                    moves[currMoveIndex++] = CreateAPIMove(startSquare, targetSquare, Move.EnPassantCaptureFlag, PieceHelper.Pawn);
                }
            }
        }

        void GeneratePromotions(Span<API.Move> moves, int startSquare, int targetSquare)
        {
            moves[currMoveIndex++] = CreateAPIMove(startSquare, targetSquare, Move.PromoteToQueenFlag, PieceHelper.Pawn);
            // Don't generate non-queen promotions in q-search
            if (generateNonCapture)
            {
                if (promotionsToGenerate == MoveGenerator.PromotionMode.All)
                {
                    moves[currMoveIndex++] = CreateAPIMove(startSquare, targetSquare, Move.PromoteToKnightFlag, PieceHelper.Pawn);
                    moves[currMoveIndex++] = CreateAPIMove(startSquare, targetSquare, Move.PromoteToRookFlag, PieceHelper.Pawn);
                    moves[currMoveIndex++] = CreateAPIMove(startSquare, targetSquare, Move.PromoteToBishopFlag, PieceHelper.Pawn);
                }
                else if (promotionsToGenerate == MoveGenerator.PromotionMode.QueenAndKnight)
                {
                    moves[currMoveIndex++] = CreateAPIMove(startSquare, targetSquare, Move.PromoteToKnightFlag, PieceHelper.Pawn);
                }
            }
        }
    }
}
