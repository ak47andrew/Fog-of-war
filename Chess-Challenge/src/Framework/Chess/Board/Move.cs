/*
32 bit (using only 17 bit) move representation.

The format is as follows (ii|fff|tttttt|ssssss)
Bits 0-5: start square index
Bits 6-11: target square index
Bits 12-14: flag (promotion type, etc)
Bit 15-16: uncomplete data for moves in the fog
*/
namespace ChessChallenge.Chess
{
    public readonly struct Move
    {
        // 32bit move value
        readonly uint moveValue;

        // Flags
        public const int NoFlag = 0b000;
        public const int EnPassantCaptureFlag = 0b001;
        public const int CastleFlag = 0b010;
        public const int PawnTwoUpFlag = 0b011;

        public const int PromoteToQueenFlag = 0b100;
        public const int PromoteToKnightFlag = 0b101;
        public const int PromoteToRookFlag = 0b110;
        public const int PromoteToBishopFlag = 0b111;

        // Uncomplete data flag
        public const int UnknownStart = 0b01;
        public const int UnknownTarget = 0b10;

        // Masks
        const uint startSquareMask = 0b00000000000111111;
        const uint targetSquareMask = 0b00000111111000000;
        const uint flagMask = 0b00111000000000000;
        const uint uncompleteDataStartMask = 0b11000000000000000;

        public Move(uint moveValue)
        {
            this.moveValue = moveValue;
        }

        public Move(int startSquare, int targetSquare, int flag = 0, int uncompleteData = 0)
        {
            moveValue = (uint)(startSquare | targetSquare << 6 | flag << 12 | uncompleteData << 15);
        }

        public uint Value => moveValue;
        public bool IsNull => moveValue == 0;

        public int StartSquareIndex => (int)(moveValue & startSquareMask);
        public int TargetSquareIndex => (int)((moveValue & targetSquareMask) >> 6);
        public bool IsPromotion => MoveFlag >= PromoteToQueenFlag;
        public bool IsEnPassant => MoveFlag == EnPassantCaptureFlag;
        public int EnpassantSquareIndex => TargetSquareIndex < 32 ? TargetSquareIndex + 8 : TargetSquareIndex - 8;
        public bool IsStartSquareUnkown => (UncompleteData & UnknownStart) != 0;
        public bool IsTargetSquareUnkown => (UncompleteData & UnknownTarget) != 0;
        public bool IsFullyUnknown => IsStartSquareUnkown && IsTargetSquareUnkown;
        public bool IsFullyKnown => !IsFullyUnknown;
        public int MoveFlag => (int)((moveValue & flagMask) >> 12);
        public int UncompleteData => (int)((moveValue & uncompleteDataStartMask) >> 15);

        public int PromotionPieceType
        {
            get
            {
                switch (MoveFlag)
                {
                    case PromoteToRookFlag:
                        return PieceHelper.Rook;
                    case PromoteToKnightFlag:
                        return PieceHelper.Knight;
                    case PromoteToBishopFlag:
                        return PieceHelper.Bishop;
                    case PromoteToQueenFlag:
                        return PieceHelper.Queen;
                    default:
                        return PieceHelper.None;
                }
            }
        }

        public static Move NullMove => new Move(0);
    }
}