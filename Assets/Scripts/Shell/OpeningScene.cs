using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace RewindNotary
{
    internal sealed class OpeningScene
    {
        private static readonly string[] PrologueKeys =
        {
            "story.prologue.city", "story.prologue.deadline",
            "story.prologue.pardon", "story.prologue.rea"
        };
        private readonly StoryRow row;
        private readonly Texture2D portraits, rooms;
        private readonly Texture2D city, warrant;
        private readonly int prologueLength;
        private string[] captions;
        private string language;
        private int index;
        private float captionTime, shotTime, duration;
        private GUIStyle subtitle, speaker, prologueSubtitle, title;

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
            rooms = Resources.Load<Texture2D>("GameArt/backgrounds/opening-rooms");
            if (row.sceneId == "intro_attic" && row.stepId == "01_wake")
            {
                prologueLength = PrologueKeys.Length;
                city = Resources.Load<Texture2D>("GameArt/backgrounds/lapis-plaza");
                warrant = Resources.Load<Texture2D>("GameArt/backgrounds/opening-warrant");
            }
            LoadCaptions();
        }

        private void LoadCaptions()
        {
            language = Loc.Language;
            shotTime = 0;
            string line = Loc.Get(row.textKey);
            captions = Array.FindAll(Regex.Split(line, @"\r?\n+|(?<=[)）])\s+(?=[^\s”»」])|(?<=[.!?])\s+(?=[^\s”»」])|(?<=[。！？])(?=[^」』”）\s])"),
                part => !string.IsNullOrWhiteSpace(part));
            for (int i = 0; i < captions.Length; i++)
                captions[i] = captions[i].Trim().Trim('“', '”', '„', '«', '»', '「', '」').Trim();
            index = 0;
            if (prologueLength > 0)
            {
                var opening = new string[PrologueKeys.Length + captions.Length];
                for (int i = 0; i < PrologueKeys.Length; i++) opening[i] = Loc.Get(PrologueKeys[i]);
                captions.CopyTo(opening, PrologueKeys.Length);
                captions = opening;
            }
            BeginCaption();
        }

        private string Caption => captions[index];
        internal bool InPrologue => index < prologueLength;
        internal int CaptionIndex => InPrologue ? index : index - prologueLength;
        private bool Direction => Caption.StartsWith("(") || Caption.StartsWith("（") || Caption.StartsWith("[") || Caption.StartsWith("【");

        private void BeginCaption()
        {
            captionTime = 0;
            float readingTime = 0;
            foreach (char ch in Caption)
                readingTime += ch >= '\u2e80' ? .095f : .050f;
            duration = 1.1f + readingTime;
        }

        internal bool Advance(float delta, bool advance)
        {
            if (language != Loc.Language) LoadCaptions();
            captionTime += delta;
            shotTime += delta;
            if (!advance && captionTime < duration) return false;
            if (index + 1 == captions.Length) return true;
            index++;
            if (index == prologueLength) shotTime = 0;
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
            }
            var color = GUI.color;
            Fill(new Rect(-Screen.width, -Screen.height, 1280 + 2 * Screen.width, 720 + 2 * Screen.height), Color.black);
            GUI.BeginGroup(new Rect(0, 0, 1280, 720));
            if (InPrologue) DrawPrologue();
            else DrawDialogue();
            GUI.EndGroup();
            GUI.color = color;
        }

        private void DrawPrologue()
        {
            bool cityShot = index < 2;
            var art = cityShot ? city : warrant;
            float drift = 12f * (1f - Mathf.Exp(-shotTime * .17f));
            GUI.color = cityShot ? new Color(.80f, .85f, .94f) : Color.white;
            GUI.DrawTexture(new Rect(-12 - drift, -7, 1320, 746), art, ScaleMode.ScaleAndCrop);
            if (index == PrologueKeys.Length - 1)
            {
                Fill(new Rect(0, 0, 1280, 106), new Color(.03f, .04f, .06f, .79f));
                GUI.Label(new Rect(60, 14, 1160, 78), Loc.Get("game.menu_title"), title);
            }
            Fill(new Rect(0, 563, 1280, 157), new Color(.03f, .04f, .06f, .94f));
            Fill(new Rect(620, 579, 40, 2), new Color(.72f, .91f, .82f));
            GUI.Label(new Rect(115, 591, 1050, 102), Caption, prologueSubtitle);
        }

        private void DrawDialogue()
        {
            bool forge = row.sceneId == "intro_forge";
            bool solo = row.sceneId == "intro_attic" && row.stepId == "01_wake";
            bool reaSpeaking = row.speakerId == "rea";
            var accent = forge ? new Color(1f, .76f, .47f) : new Color(.72f, .91f, .82f);
            float drift = 7f * (1f - Mathf.Exp(-shotTime * .18f));
            GUI.color = Color.white;
            GUI.DrawTextureWithTexCoords(new Rect(-9 - drift, 0, 1310, 656), rooms, new Rect(0, forge ? 0 : .5f, 1, .5f));
            DrawActor(0, solo ? 270 : 200, reaSpeaking);
            if (!solo) DrawActor(forge ? 2 : 1, 1030, !reaSpeaking);

            var content = new GUIContent(Caption.Trim());
            float textHeight = subtitle.CalcHeight(content, 1072);
            float top = Mathf.Min(540f, 720f - textHeight - 83f);
            Fill(new Rect(0, top, 1280, 720 - top), new Color(.056f, .060f, .070f, .97f));
            Fill(new Rect(94, top + 22, 34, 3), accent);
            speaker.normal.textColor = accent;
            GUI.Label(new Rect(139, top + 8, 990, 38),
                Loc.Get(Direction ? "area.room." + row.room : "area.speaker." + row.speakerId), speaker);
            subtitle.normal.textColor = Direction ? new Color(.83f, .84f, .81f) : new Color(.99f, .97f, .92f);
            GUI.Label(new Rect(96, top + 51, 1072, textHeight + 8), content, subtitle);
        }

        private void DrawActor(int column, float center, bool active)
        {
            float entrance = Mathf.SmoothStep(0, 1, shotTime / .45f);
            float size = active ? 482 : 428;
            float breath = Mathf.Sin(shotTime * 2.2f + column) * (active ? 2.1f : 1f);
            float x = center - size * .5f + (column == 0 ? -14 : 14) * (1f - entrance);
            bool speaking = active && !Direction && captionTime > .20f && captionTime < duration - .50f;
            bool mouthOpen = speaking && Mathf.Sin(captionTime * 19f) + .55f * Mathf.Sin(captionTime * 8f) > .15f;
            GUI.color = active ? Color.white : new Color(.70f, .72f, .75f);
            GUI.DrawTextureWithTexCoords(new Rect(x, 562 - size + breath, size, size), portraits,
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
