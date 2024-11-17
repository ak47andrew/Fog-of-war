using Raylib_cs;
using System;
using System.Numerics;
using System.Runtime.InteropServices;
using static ChessChallenge.Application.FileHelper;

namespace ChessChallenge.Application
{
    public static class UIHelper
    {
        static readonly bool SDF_Enabled = false;
        const string fontName = "OPENSANS-SEMIBOLD.TTF";
        const int referenceResolution = 1920;

        static Font font;
        static Font fontSdf;
        static Shader shader;

        public enum AlignH
        {
            Left,
            Centre,
            Right
        }
        public enum AlignV
        {
            Top,
            Centre,
            Bottom
        }

        static UIHelper()
        {
            if (SDF_Enabled)
            {
                unsafe
                {
                    const int baseSize = 64;
                    uint fileSize = 0;
                    var fileData = Raylib.LoadFileData(GetResourcePath("Fonts", fontName), ref fileSize);
                    Font fontSdf = default;
                    fontSdf.baseSize = baseSize;
                    fontSdf.glyphCount = 95;
                    fontSdf.glyphs = Raylib.LoadFontData(fileData, (int)fileSize, baseSize, null, 0, FontType.FONT_SDF);

                    Image atlas = Raylib.GenImageFontAtlas(fontSdf.glyphs, &fontSdf.recs, 95, baseSize, 0, 1);
                    fontSdf.texture = Raylib.LoadTextureFromImage(atlas);
                    Raylib.UnloadImage(atlas);
                    Raylib.UnloadFileData(fileData);

                    Raylib.SetTextureFilter(fontSdf.texture, TextureFilter.TEXTURE_FILTER_BILINEAR);
                    UIHelper.fontSdf = fontSdf;

                }
                shader = Raylib.LoadShader("", GetResourcePath("Fonts", "sdf.fs"));
            }
            font = Raylib.LoadFontEx(GetResourcePath("Fonts", fontName), 128, null, 0);

        }

        public static void DrawText(string text, Vector2 pos, int size, int spacing, Color col, AlignH alignH = AlignH.Left, AlignV alignV = AlignV.Centre)
        {
            Vector2 boundSize = Raylib.MeasureTextEx(font, text, size, spacing);
            float offsetX = alignH == AlignH.Left ? 0 : (alignH == AlignH.Centre ? -boundSize.X / 2 : -boundSize.X);
            float offsetY = alignV == AlignV.Top ? 0 : (alignV == AlignV.Centre ? -boundSize.Y / 2 : -boundSize.Y);
            Vector2 offset = new(offsetX, offsetY);

            if (SDF_Enabled)
            {
                Raylib.BeginShaderMode(shader);
                Raylib.DrawTextEx(fontSdf, text, pos + offset, size, spacing, col);
                Raylib.EndShaderMode();
            }
            else
            {
                Raylib.DrawTextEx(font, text, pos + offset, size, spacing, col);
            }
        }

        public static bool Button(string text, Vector2 centre, Vector2 size)
        {
            Rectangle rec = new(centre.X - size.X / 2, centre.Y - size.Y / 2, size.X, size.Y);

            Color normalCol = new(40, 40, 40, 255);
            Color hoverCol = new(3, 173, 252, 255);
            Color pressCol = new(2, 119, 173, 255);

            bool mouseOver = MouseInRect(rec);
            bool pressed = mouseOver && Raylib.IsMouseButtonDown(MouseButton.MOUSE_BUTTON_LEFT);
            bool pressedThisFrame = pressed && Raylib.IsMouseButtonPressed(MouseButton.MOUSE_BUTTON_LEFT);
            Color col = mouseOver ? (pressed ? pressCol : hoverCol) : normalCol;

            Raylib.DrawRectangleRec(rec, col);
            Color textCol = mouseOver ? Color.WHITE : new Color(180, 180, 180, 255);
            int fontSize = ScaleInt(32);

            DrawText(text, centre, fontSize, 1, textCol, AlignH.Centre);

            return pressedThisFrame;
        }

        static bool MouseInRect(Rectangle rec)
        {
            Vector2 mousePos = Raylib.GetMousePosition();
            return mousePos.X >= rec.x && mousePos.Y >= rec.y && mousePos.X <= rec.x + rec.width && mousePos.Y <= rec.y + rec.height;
        }

        public static float Scale(float val, int referenceResolution = referenceResolution)
        {
            return Raylib.GetScreenWidth() / (float)referenceResolution * val;
        }

        public static int ScaleInt(int val, int referenceResolution = referenceResolution)
        {
            return (int)Math.Round(Raylib.GetScreenWidth() / (float)referenceResolution * val);
        }

        public static Vector2 Scale(Vector2 vec, int referenceResolution = referenceResolution)
        {
            float x = Scale(vec.X, referenceResolution);
            float y = Scale(vec.Y, referenceResolution);
            return new Vector2(x, y);
        }

        public static void Release()
        {
            Raylib.UnloadFont(font);
            if (SDF_Enabled)
            {
                Raylib.UnloadFont(fontSdf);
                Raylib.UnloadShader(shader);
            }
        }

        public class InputBox
        {
            public Rectangle LargeBox { get; private set; }
            public Rectangle SmallBox { get; private set; }
            public string Text { get; private set; } = "";
            public bool IsFocused { get; private set; } = false;
            public bool isScaled = false;

            private Color boxColor;
            private Color textColor;
            private Color borderColor;
            private int fontSize;
            private int backspaceCooldown = 0;

            public InputBox(Rectangle large, Rectangle small, int fontSize = 20, Color? boxColor = null, Color? textColor = null, Color? borderColor = null)
            {
                LargeBox = large;
                SmallBox = small;
                this.fontSize = fontSize;

                this.boxColor = boxColor ?? Color.LIGHTGRAY;
                this.textColor = textColor ?? Color.BLACK;
                this.borderColor = borderColor ?? Color.GRAY;
            }

            public void Update()
            {
                backspaceCooldown--;
                // Check for focus when the box is clicked
                if (Raylib.IsMouseButtonPressed(MouseButton.MOUSE_LEFT_BUTTON))
                {
                    Vector2 mousePosition = Raylib.GetMousePosition();
                    IsFocused = Raylib.CheckCollisionPointRec(mousePosition, CurrentBox);
                }
                else if (Raylib.IsMouseButtonPressed(MouseButton.MOUSE_LEFT_BUTTON))
                {
                    // Lose focus if clicked outside the box
                    IsFocused = false;
                }

                // Handle keyboard input if focused
                if (IsFocused)
                {
                    HandleTextInput();
                    HandleCopyPaste();
                }
            }

            private void HandleTextInput()
            {
                int key = Raylib.GetCharPressed();

                while (key > 0)
                {
                    if (key >= 32 && key <= 126) // Allow printable ASCII
                    {
                        Text += (char)key;
                    }
                    key = Raylib.GetCharPressed();
                }

                // Handle backspace
                if (Raylib.IsKeyDown(KeyboardKey.KEY_BACKSPACE) && backspaceCooldown <= 0 && Text.Length > 0)
                {
                    Text = Text.Substring(0, Text.Length - 1);
                    backspaceCooldown = 5;
                }
            }

            private static string GetSafeClipboardText()
            {
                unsafe
                {
                    sbyte* clipboardTextPointer = Raylib.GetClipboardText();
                    return Marshal.PtrToStringAnsi((IntPtr)clipboardTextPointer) ?? "";
                }
            }


            private void HandleCopyPaste()
            {
                // Copy text to clipboard (CTRL+C)
                if (Raylib.IsKeyDown(KeyboardKey.KEY_LEFT_CONTROL) && Raylib.IsKeyPressed(KeyboardKey.KEY_C))
                {
                    Raylib.SetClipboardText(Text);
                }

                // Paste text from clipboard (CTRL+V)
                if (Raylib.IsKeyDown(KeyboardKey.KEY_LEFT_CONTROL) && Raylib.IsKeyPressed(KeyboardKey.KEY_V))
                {
                    string clipboardText = GetSafeClipboardText();
                    if (!string.IsNullOrEmpty(clipboardText))
                    {
                        Text += clipboardText;
                    }
                }
            }

            public void Draw()
            {
                Rectangle box = CurrentBox;
                
                // Draw the input box
                Raylib.DrawRectangleRec(box, boxColor);
                Raylib.DrawRectangleLinesEx(box, 2, IsFocused ? Color.BLUE : borderColor);

                // Draw the text
                Raylib.DrawText(Text, (int)box.x + 5, (int)(box.y + (box.height - fontSize) / 2), fontSize, textColor);

                // Draw a blinking cursor when focused
                if (IsFocused && DateTime.Now.Millisecond / 500 % 2 == 0)
                {
                    int textWidth = Raylib.MeasureText(Text, fontSize);
                    Raylib.DrawLine(
                        (int)box.x + 5 + textWidth,
                        (int)(box.y + (box.height - fontSize) / 2),
                        (int)box.x + 5 + textWidth,
                        (int)(box.y + (box.height - fontSize) / 2 + fontSize),
                        textColor
                    );
                }
            }

            private Rectangle CurrentBox => (Program.GetSavedWindowSize() == Settings.ScreenSizeBig && !isScaled) || 
                                            (Program.GetSavedWindowSize() != Settings.ScreenSizeBig && isScaled)
                                            ? LargeBox : SmallBox;
        }
    }
}
