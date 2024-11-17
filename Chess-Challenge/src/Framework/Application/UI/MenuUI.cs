using Raylib_cs;
using System.Numerics;
using System;
using System.IO;
using static ChessChallenge.Application.UIHelper;

namespace ChessChallenge.Application
{
    public static class MenuUI
    {
        static InputBox inputBoxWhite = new InputBox(new Rectangle(130, 210, 260, 55), new Rectangle(65, 105, 130, 27.5f));
        // static InputBox inputBoxBlack = new InputBox(new Rectangle(130, 310, 260, 55));
        static InputBox inputBoxBlack = new InputBox(new Rectangle(130, 310, 260, 55), new Rectangle(65, 155, 130, 27.5f));

        public static void DrawButtons(ChallengeController controller)
        {
            Vector2 buttonPos = Scale(new Vector2(260, 310));
            Vector2 buttonSize = Scale(new Vector2(260, 55));
            float spacing = buttonSize.Y * 1.2f;
            float breakSpacing = spacing * 0.6f;

            Raylib.DrawText("White:", ScaleInt(130), ScaleInt(180), ScaleInt(20), Color.WHITE); // Label for White
            Raylib.DrawText("Black:", ScaleInt(130), ScaleInt(280), ScaleInt(20), Color.WHITE); // Label for Black

            // Game Buttons
            // if (NextButtonInRow("Human vs MyBot", ref buttonPos, spacing, buttonSize))
            // {
            //     var whiteType = controller.HumanWasWhiteLastGame ? ChallengeController.PlayerType.MyBot : ChallengeController.PlayerType.Human;
            //     var blackType = !controller.HumanWasWhiteLastGame ? ChallengeController.PlayerType.MyBot : ChallengeController.PlayerType.Human;
            //     controller.StartNewGame(whiteType, blackType);
            // }
            // if (NextButtonInRow("MyBot vs MyBot", ref buttonPos, spacing, buttonSize))
            // {
            //     controller.StartNewBotMatch(ChallengeController.PlayerType.MyBot, ChallengeController.PlayerType.MyBot);
            // }
            // if (NextButtonInRow("MyBot vs EvilBot", ref buttonPos, spacing, buttonSize))
            // {
            //     controller.StartNewBotMatch(ChallengeController.PlayerType.MyBot, ChallengeController.PlayerType.EvilBot);
            // }

            buttonPos.Y += spacing;
            inputBoxWhite.Draw();
            inputBoxWhite.Update();
            inputBoxBlack.Draw();
            inputBoxBlack.Update();
            buttonPos.Y += spacing / 2;

            if (NextButtonInRow("Play", ref buttonPos, spacing, buttonSize)){
                controller.StartNewBotMatch(inputBoxWhite.Text, inputBoxBlack.Text);
            }

            // Page buttons
            buttonPos.Y += breakSpacing;

            if (NextButtonInRow("Save Games", ref buttonPos, spacing, buttonSize))
            {
                string pgns = controller.AllPGNs;
                string directoryPath = Path.Combine(FileHelper.AppDataPath, "Games");
                Directory.CreateDirectory(directoryPath);
                string fileName = FileHelper.GetUniqueFileName(directoryPath, "games", ".txt");
                string fullPath = Path.Combine(directoryPath, fileName);
                File.WriteAllText(fullPath, pgns);
                ConsoleHelper.Log("Saved games to " + fullPath, false, ConsoleColor.Blue);
            }
            if (NextButtonInRow("Rules & Help", ref buttonPos, spacing, buttonSize))
            {
                FileHelper.OpenUrl("https://github.com/SebLague/Chess-Challenge");
            }
            if (NextButtonInRow("Documentation", ref buttonPos, spacing, buttonSize))
            {
                FileHelper.OpenUrl("https://seblague.github.io/chess-coding-challenge/documentation/");
            }
            if (NextButtonInRow("Submission Page", ref buttonPos, spacing, buttonSize))
            {
                FileHelper.OpenUrl("https://forms.gle/6jjj8jxNQ5Ln53ie6");
            }

            // Window and quit buttons
            buttonPos.Y += breakSpacing;

            bool isBigWindow = Raylib.GetScreenWidth() > Settings.ScreenSizeSmall.X;
            string windowButtonName = isBigWindow ? "Smaller Window" : "Bigger Window";
            if (NextButtonInRow(windowButtonName, ref buttonPos, spacing, buttonSize))
            {
                Program.SetWindowSize(isBigWindow ? Settings.ScreenSizeSmall : Settings.ScreenSizeBig);
                inputBoxBlack.isScaled = true;
                inputBoxWhite.isScaled = true;
            }
            if (NextButtonInRow("Exit (ESC)", ref buttonPos, spacing, buttonSize))
            {
                Environment.Exit(0);
            }

            bool NextButtonInRow(string name, ref Vector2 pos, float spacingY, Vector2 size)
            {
                bool pressed = Button(name, pos, size);
                pos.Y += spacingY;
                return pressed;
            }
        }
    }
}