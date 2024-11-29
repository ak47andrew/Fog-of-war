using ChessChallenge.Chess;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Net.WebSockets;
using System.Threading.Tasks;
using System.Text;
using System.Net.NetworkInformation;
using System.Net;
using System.Linq;

namespace ChessChallenge.Application
{
    public class UCIPlayer : IDisposable
    {
        private readonly string enginePath;
        private readonly string engineName;
        private readonly Process engineProcess;
        private readonly ClientWebSocket webSocket;
        private readonly int port;
        private Board prevBoard;
        private readonly CancellationTokenSource cancellationSource;

        public bool IsBroken => engineProcess == null || engineProcess.HasExited;

        private int FindRandomUnusedPort()
        {
            // Get list of used ports
            var usedPorts = IPGlobalProperties.GetIPGlobalProperties()
                .GetActiveTcpListeners()
                .ToArray()
                .Select(x => x.Port)
                .ToHashSet();

            // Find random unused port between 49152-65535 (dynamic port range)
            Random rnd = new Random();
            int port;
            do
            {
                port = rnd.Next(49152, 65536);
            } while (usedPorts.Contains(port));

            return port;
        }

        public UCIPlayer(string path)
        {
            enginePath = path;
            engineName = Path.GetFileNameWithoutExtension(path);
            webSocket = new ClientWebSocket();
            cancellationSource = new CancellationTokenSource();
            port = FindRandomUnusedPort();

            try
            {
                // Start process with port as argument
                engineProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = enginePath,
                        Arguments = port.ToString(), // Pass port as argument
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                engineProcess.Start();

                // Connect WebSocket
                ConnectWebSocket().Wait();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to start engine process: {e.Message}");
                Dispose();
            }
        }

        private async Task ConnectWebSocket()
        {
            // Wait briefly for process to start listening
            await Task.Delay(100);
            
            // Connect to WebSocket endpoint
            var uri = new Uri($"ws://localhost:{port}");
            await webSocket.ConnectAsync(uri, cancellationSource.Token);
        }

        private async Task SendMessageAsync(string message)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            await webSocket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                cancellationSource.Token);
        }

        private async Task<string> ReceiveMessageAsync(TimeSpan timeout)
        {
            var buffer = new byte[4096];
            var received = new StringBuilder();
            
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationSource.Token);
            cts.CancelAfter(timeout);

            try
            {
                while (true)
                {
                    var result = await webSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        cts.Token);

                    received.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

                    if (result.EndOfMessage)
                        break;
                }

                return received.ToString();
            }
            catch (OperationCanceledException)
            {
                return null;
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
                    fen = FenUtility.CurrentFen(board),
                    prev = prevBoard != null ? FenUtility.CurrentFen(prevBoard) : "",
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
                SendMessageAsync(jsonState).Wait();

                var timeout = TimeSpan.FromMilliseconds(timer.MillisecondsRemaining);
                string moveStr = ReceiveMessageAsync(timeout).Result;

                if (!string.IsNullOrEmpty(moveStr))
                {
                    prevBoard = new Board(board);
                    return ParseMove(moveStr, board);
                }

                Console.WriteLine("Engine timed out");
                return Move.NullMove;
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
                Random rng = new Random();
                MoveGenerator movegen = new();
                Move[] moves = movegen.GenerateMoves(board).ToArray();
                return moves[rng.Next(moves.Length)];
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
                cancellationSource.Cancel();
                
                if (webSocket.State == WebSocketState.Open)
                {
                    webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disposing", CancellationToken.None).Wait();
                }
                webSocket.Dispose();
                
                if (engineProcess != null && !engineProcess.HasExited)
                {
                    engineProcess.Kill();
                    engineProcess.Dispose();
                }

                cancellationSource.Dispose();
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