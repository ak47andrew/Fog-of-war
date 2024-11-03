using System.Collections.Generic;
using ChessChallenge.API;

public class FogOfWar
{
    Board board;
    List<Square> FoW = new();

    public FogOfWar(Board board){
        this.board = board;
    }

    public void AddFogOfWar() {
        GenerateFogOfWar();
        FixPieceList();
        // FEN
        // King square
        // GetPiece
        // Castle
        // SquareIsAttackedByOpponent
        // ZobristKey
    }

    void GenerateFogOfWar(){
        foreach (PieceList l in board.GetAllPieceLists())
        {
            foreach (Piece piece in l)
            {
                if (piece.IsWhite != board.IsWhiteToMove)
                    break;
                FoW.Add(piece.Square);
            }
        }

        foreach (Move move in board.GetLegalMoves())
        {
            FoW.Add(move.TargetSquare);
        }
    }

    void FixPieceList(){
        for (int i = 0; i < board.GetAllPieceLists().Length; i++)
        {
            var l = board.GetAllPieceLists()[i];
            List<Piece> p = new();
            foreach (Piece piece in l)
            {
                if (FoW.Contains(piece.Square))
                    p.Add(piece);
            }
            board.UpdatePieceList(i, p.ToArray());
        }
    }
}