using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace RewindNotary
{
    internal sealed class OpeningScene
    {
        internal enum PrologueShot { City, Bureau, Warrant, Breakfast, Attic }

        private static readonly (string key, PrologueShot shot)[] Prologue =
        {
            ("city", PrologueShot.City),
            ("craft", PrologueShot.City),
            ("record", PrologueShot.City),
            ("deadline", PrologueShot.Bureau),
            ("order", PrologueShot.Bureau),
            ("condition", PrologueShot.Warrant),
            ("pardon", PrologueShot.Warrant),
            ("rea", PrologueShot.Warrant),
            ("table", PrologueShot.Breakfast),
            ("place", PrologueShot.Breakfast),
            ("dawn", PrologueShot.Attic)
        };
        private const float CaptionBandTop = 536f;
        private readonly StoryRow row;
        private readonly Texture2D portraits, gestures, rooms, dialogueRoom;
        private readonly Texture2D city, warrant, vignettes;
        private readonly int prologueLength;
        private string[] captions;
        private string language;
        private int index;
        private float captionTime, duration;
        private GUIStyle subtitle, speaker, prologueSubtitle, title, continueHint;

        internal static OpeningScene For(StoryRow row)
        {
            switch (row.sceneId)
            {
                case "intro_attic":
                case "intro_board":
                case "intro_forge": return new OpeningScene(row);
                default: return null;
            }
        }

        private OpeningScene(StoryRow row)
        {
            this.row = row;
            portraits = Resources.Load<Texture2D>("GameArt/sprites/opening-portraits");
            gestures = Resources.Load<Texture2D>("GameArt/sprites/opening-gestures");
            rooms = Resources.Load<Texture2D>("GameArt/backgrounds/opening-rooms");
            dialogueRoom = row.sceneId == "intro_board" || row.stepId == "04_receive_copy" || row.stepId == "05_read_copy"
                ? Resources.Load<Texture2D>("GameArt/backgrounds/opening-handoff")
                : rooms;
            if (row.sceneId == "intro_attic" && row.stepId == "01_wake")
            {
                prologueLength = Prologue.Length;
                city = Resources.Load<Texture2D>("GameArt/backgrounds/lapis-plaza");
                warrant = Resources.Load<Texture2D>("GameArt/backgrounds/opening-warrant");
                vignettes = Resources.Load<Texture2D>("GameArt/backgrounds/opening-vignettes");
            }
            LoadCaptions();
        }

        private void LoadCaptions()
        {
            language = Loc.Language;
            var opening = new List<string>();
            for (int i = 0; i < prologueLength; i++) opening.Add(Loc.Get("story.prologue." + Prologue[i].key));
            foreach (string line in Loc.Get(row.textKey).Split('\n'))
            {
                string speech = TrimQuotes(line);
                var direction = Regex.Match(speech, @"^(?:\([^)]*\)|（[^）]*）|\[[^\]]*\]|【[^】]*】)");
                if (direction.Success)
                {
                    opening.Add(direction.Value);
                    speech = TrimQuotes(speech.Substring(direction.Length));
                }
                foreach (string sentence in Regex.Split(speech, @"(?<=[.!?])\s+(?=[^\s”»」])|(?<=[。！？])(?=[^」』”）\s])"))
                {
                    string caption = TrimQuotes(sentence);
                    if (caption.Length != 0) opening.Add(caption);
                }
            }
            captions = opening.ToArray();
            index = 0;
            BeginCaption();
        }

        private static string TrimQuotes(string text) => text.Trim().Trim('“', '”', '„', '«', '»', '「', '」').Trim();

        private string Caption => captions[index];
        internal bool InPrologue => index < prologueLength;
        internal int CaptionIndex => index - (InPrologue ? 0 : prologueLength);
        internal PrologueShot CurrentShot => Prologue[index].shot;
        private bool Direction => Caption.StartsWith("(") || Caption.StartsWith("（") || Caption.StartsWith("[") || Caption.StartsWith("【");

        private void BeginCaption()
        {
            captionTime = 0;
            float readingTime = 0;
            foreach (char ch in Caption)
                readingTime += ch >= '\u2e80' ? .095f : .050f;
            duration = (InPrologue ? 1.8f : 1.1f) + readingTime;
        }

        internal bool Advance(float delta, bool advance)
        {
            if (language != Loc.Language) LoadCaptions();
            captionTime += delta;
            if (!advance) return false;
            if (index + 1 == captions.Length) return true;
            index++;
            BeginCaption();
            return false;
        }

        internal void Draw(Font font)
        {
            if (subtitle == null)
            {
                subtitle = new GUIStyle(GUI.skin.label) { font = font, fontSize = 30, wordWrap = true, alignment = TextAnchor.UpperLeft, richText = false };
                speaker = new GUIStyle(subtitle) { fontSize = 22, fontStyle = FontStyle.Bold };
                prologueSubtitle = new GUIStyle(subtitle) { alignment = TextAnchor.MiddleCenter };
                prologueSubtitle.normal.textColor = new Color(.99f, .97f, .92f);
                title = new GUIStyle(prologueSubtitle) { fontSize = 42, fontStyle = FontStyle.Bold };
                continueHint = new GUIStyle(subtitle) { fontSize = 15, alignment = TextAnchor.MiddleRight };
                continueHint.normal.textColor = new Color(.76f, .85f, .82f);
            }
            var color = GUI.color;
            Fill(new Rect(-Screen.width, -Screen.height, 1280 + 2 * Screen.width, 720 + 2 * Screen.height), Color.black);
            GUI.BeginGroup(new Rect(0, 0, 1280, 720));
            if (InPrologue) DrawPrologue();
            else DrawDialogue();
            GUI.Label(new Rect(720, 686, 448, 25),
                (GameInput.UsingGamepad ? "[A]" : "Enter") + "  ·  " + Loc.Get("ui.continue"), continueHint);
            GUI.EndGroup();
            GUI.color = color;
        }

        private void DrawPrologue()
        {
            var frame = new Rect(0, 0, 1280, 656);
            GUI.color = Color.white;
            switch (CurrentShot)
            {
                case PrologueShot.City:
                    GUI.DrawTexture(frame, city, ScaleMode.ScaleAndCrop);
                    break;
                case PrologueShot.Bureau:
                    GUI.DrawTextureWithTexCoords(frame, vignettes, new Rect(0, .5f, 1, .5f));
                    break;
                case PrologueShot.Warrant:
                    GUI.DrawTexture(frame, warrant, ScaleMode.ScaleAndCrop);
                    break;
                case PrologueShot.Breakfast:
                    GUI.DrawTextureWithTexCoords(frame, vignettes, new Rect(0, 0, 1, .5f));
                    break;
                case PrologueShot.Attic:
                    GUI.DrawTextureWithTexCoords(frame, rooms, new Rect(0, .5f, 1, .5f));
                    break;
            }
            if (CurrentShot == PrologueShot.Attic)
            {
                Fill(new Rect(0, 0, 1280, 106), new Color(.03f, .04f, .06f, .79f));
                GUI.Label(new Rect(60, 14, 1160, 78), Loc.Get("game.menu_title"), title);
            }
            Fill(new Rect(0, CaptionBandTop, 1280, 720 - CaptionBandTop), new Color(.03f, .04f, .06f, .97f));
            Fill(new Rect(620, CaptionBandTop + 24, 40, 2), new Color(.72f, .91f, .82f));
            GUI.Label(new Rect(115, CaptionBandTop + 38, 1050, 107), Caption, prologueSubtitle);
        }

        private void DrawDialogue()
        {
            bool forge = row.sceneId == "intro_forge";
            bool solo = row.sceneId == "intro_attic" && row.stepId == "01_wake";
            bool reaSpeaking = row.speakerId == "rea";
            var accent = forge ? new Color(1f, .76f, .47f) : new Color(.72f, .91f, .82f);
            GUI.color = Color.white;
            GUI.DrawTextureWithTexCoords(new Rect(0, 0, 1280, 656), dialogueRoom, new Rect(0, forge ? 0 : .5f, 1, .5f));
            bool reaGesture = (solo && CaptionIndex > 0) || row.stepId == "03_rea_question" || row.stepId == "05_read_copy";
            bool partnerGesture = row.stepId == "04_receive_copy" || row.stepId == "02_three_notches" || row.stepId == "04_ethan_truth";
            DrawActor(0, 236, reaSpeaking, reaGesture);
            if (!solo) DrawActor(forge ? 2 : 1, 1044, !reaSpeaking, partnerGesture);

            var content = new GUIContent(Caption.Trim());
            float top = CaptionBandTop;
            Fill(new Rect(0, top, 1280, 720 - top), new Color(.056f, .060f, .070f, .97f));
            Fill(new Rect(94, top + 22, 34, 3), accent);
            speaker.normal.textColor = accent;
            GUI.Label(new Rect(139, top + 8, 990, 38),
                Loc.Get(Direction ? "area.room." + row.room : "area.speaker." + row.speakerId), speaker);
            subtitle.normal.textColor = Direction ? new Color(.83f, .84f, .81f) : new Color(.99f, .97f, .92f);
            subtitle.fontSize = 30;
            while (subtitle.fontSize > 18 && subtitle.CalcHeight(content, 1072) > 100)
                subtitle.fontSize -= 2;
            GUI.Label(new Rect(96, top + 51, 1072, 100), content, subtitle);
        }

        private void DrawActor(int column, float center, bool active, bool gesture)
        {
            const float size = 462f;
            bool speaking = active && !Direction && captionTime > .20f && captionTime < duration - .50f;
            bool mouthOpen = speaking && Mathf.Sin(captionTime * 14f) + .55f * Mathf.Sin(captionTime * 6f) > .15f;
            GUI.color = active ? Color.white : new Color(.84f, .85f, .87f);
            GUI.DrawTextureWithTexCoords(new Rect(center - size * .5f, 550 - size, size, size), gesture ? gestures : portraits,
                new Rect(column / 3f, mouthOpen ? 0 : .5f, 1f / 3f, .5f));
            GUI.color = Color.white;
        }

        private static void Fill(Rect rect, Color tint)
        {
            GUI.color = tint;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
