using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using ChessChallenge.API;
using System.Text;

namespace ChessChallenge.Application
{
    public class UCIBot : API.IChessBot{
        public string command;
        public string name;

        Process process;
        StreamWriter stdin;
        StreamReader stdout;
        StreamReader stderr;
        Board? previousPos = null;

        public void Stop(){
            process.Kill();
        }

        static string FormatOptions(Dictionary<string, string> options) {
            return string.Join(' ', options.Select(kvp => $"{kvp.Key} {kvp.Value}"));
        }
        
        // Unused for now, but I'll let it be here jic
        // static Dictionary<string, string> GetOptions(string options) {
        //     string[] p =  options.Split(' ');
        //     return p
        //             .Where((_, i) => i % 2 == 0)
        //             .ToDictionary(key => key, key => p[Array.IndexOf(p, key) + 1]);
        // }

        public static int[] GetVisible(Board board) {
            HashSet<int> visible = new();
            Move[] moves = board.GetLegalMoves();
            foreach (var move in moves)
            {
                visible.Add(move.TargetSquare.Index);
            }
            return visible.ToArray();
        }

        public static string GetFowFen(Board board)
        {
            // Step 1: Get the first part of the FEN
            string fen = board.GetFenString().Split(' ')[0];
            
            // Step 2: Get visible squares (assumes this method returns an array of indexes 0-63)
            int[] visibleSquares = GetVisible(board);
            
            // Step 3: Build the new FEN with obscured squares
            StringBuilder modifiedFen = new();
            int squareIndex = 0;
            
            foreach (char c in fen)
            {
                if (char.IsDigit(c)) // Skip empty squares
                {
                    int emptySquares = c - '0';
                    for (int i = 0; i < emptySquares; i++)
                    {
                        modifiedFen.Append(visibleSquares.Contains(squareIndex) ? '1' : '?');
                        squareIndex++;
                    }
                }
                else if (c == '/') // Row separator
                {
                    modifiedFen.Append('/');
                }
                else // Piece characters
                {
                    modifiedFen.Append(visibleSquares.Contains(squareIndex) ? c : '?');
                    squareIndex++;
                }
            }

            // Step 4: Add "-W" or "-B" based on the current turn
            modifiedFen.Append(board.IsWhiteToMove ? "-W" : "-B");
            modifiedFen.Append(board.HasKingsideCastleRight(board.IsWhiteToMove) ? "K" : "-");
            modifiedFen.Append(board.HasQueensideCastleRight(board.IsWhiteToMove) ? "Q" : "-");
            
            // Step 5: Return the modified FEN
            return Regex.Replace(modifiedFen.ToString(), "1+", match => match.Length.ToString());
        }

        public UCIBot(string uciCommand){
            command = uciCommand;
            string[] split = command.Split(' ');
            name = split[0];
            process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = Path.Combine(FileHelper.GetBotsPath(), split[0]),
                    Arguments = string.Join(' ', split.Skip(1).Take(split.Length - 1)),
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            stdin = process.StandardInput;
            stdout = process.StandardOutput;
            stderr = process.StandardError;
        }

        ~UCIBot(){
            Stop();
        }

        void handleError(){
            string op = "";
            while (stderr.Peek() > -1){
                op += stderr.ReadLine();
            }

            ConsoleHelper.Log("An error occurred while bot was thinking.\n" + op, true, ConsoleColor.Red);
        }

        public Move Think(Board board, Timer timer){ // Fixed 100%
            Dictionary<string, string> args = new Dictionary<string, string>(){
                {"fen", GetFowFen(board)},
            };
            if (previousPos != null){
                args.TryAdd("prev", GetFowFen(board));
            }
            args.TryAdd("time", timer.MillisecondsRemaining.ToString());
            args.TryAdd("inc", timer.IncrementMilliseconds.ToString());


            if (process.HasExited)
            {
                throw new InvalidOperationException("Process has exited unexpectedly.");
            }

            stdin.WriteLine(FormatOptions(args));
            stdin.Flush();
            Console.WriteLine("> " + FormatOptions(args));

            string move = "";
            while (stdout.Peek() == -1){
                if (stderr.Peek() > -1){
                    handleError();
                    break;
                }
            }

            if (stdout.Peek() > -1){
                move = stdout.ReadLine();
            }
            Console.WriteLine("< " + move);

            previousPos = board;

            return board.GetLegalMoves()[0];

            // return new Move(move, board);
        }
    }
}
