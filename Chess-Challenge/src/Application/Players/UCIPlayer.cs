using ChessChallenge.Chess;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;

namespace ChessChallenge.Application
{
    public class UCIPlayer : IDisposable
    {
        private readonly string enginePath;
        private readonly string engineName;
        private readonly Process engineProcess;
        private readonly StreamWriter stdin;
        private readonly StreamReader stdout;
        private readonly StreamReader stderr;
        private Board prevBoard;

        public bool IsBroken => engineProcess == null || engineProcess.HasExited;

        public UCIPlayer(string path)
        {
            enginePath = path;
            engineName = Path.GetFileNameWithoutExtension(path);
            
            try
            {
                engineProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = enginePath,
                        UseShellExecute = false,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                engineProcess.Start();
                stdin = engineProcess.StandardInput;
                stdout = engineProcess.StandardOutput;
                stderr = engineProcess.StandardError;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to start engine process: {e.Message}");
                Dispose();
            }
        }

        public Move Think(Board board, Timer timer)
        {
            if (IsBroken)
                return Move.NullMove;

            try
            {
                var gameState = new
                {
                    fen = FenUtility.CurrentFoWFen(board),
                    prev = prevBoard != null ? FenUtility.CurrentFoWFen(prevBoard) : "",
                    time = timer.MillisecondsRemaining,
                    inc = timer.IncrementMilliseconds,
                    isWhite = board.IsWhiteToMove,
                    castling = new
                    {
                        queenside = board.IsWhiteToMove ? board.WhiteQueenside : board.BlackQueenside,
                        kingside = board.IsWhiteToMove ? board.WhiteKingside : board.BlackKingside
                    }
                };

                string jsonState = JsonSerializer.Serialize(gameState);
                stdin.WriteLine(jsonState);
                stdin.Flush();

                string? moveStr = stdout.ReadLine();
                if (!string.IsNullOrEmpty(moveStr))
                {
                    prevBoard = new Board(board);
                    Move move = ParseMove(moveStr, board);
                    return move;
                } else {
                    Console.WriteLine("EOF? Something strage happend");
                    return Move.NullMove;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error during engine communication: {e.Message}");
                Dispose();
                return Move.NullMove;
            }
        }

        private Move ParseMove(string moveStr, Board board)
        {
            try 
            {
                return MoveUtility.GetMoveFromUCIName(moveStr, board);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error parsing move {moveStr}: {e.Message}");
                return Move.NullMove;
            }
        }

        public void Dispose()
        {
            try
            {
                stdin?.Close();
                stdout?.Close();
                stderr?.Close();
                
                if (engineProcess != null && !engineProcess.HasExited)
                {
                    engineProcess.Kill();
                    engineProcess.Dispose();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error during cleanup: {e.Message}");
            }
        }

        ~UCIPlayer()
        {
            Dispose();
        }
    }
}