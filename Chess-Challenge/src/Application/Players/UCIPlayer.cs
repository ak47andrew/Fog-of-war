using ChessChallenge.Chess;

namespace ChessChallenge.Application
{
    public class UCIPlayer {
        public Move Think(Board board, Timer timer){
            MoveGenerator movegen = new();
            Move[] moves = movegen.GenerateMoves(board).ToArray();
            return moves[0];
            // TODO: Template
        }
    }
}
