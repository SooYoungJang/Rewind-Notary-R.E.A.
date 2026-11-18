using System;
using UnityEngine;

namespace RewindNotary
{
    public sealed partial class GameShell
    {
        private static float Paragraph(float x, float y, float width, string value, GUIStyle style, bool draw)
        {
            float height = Mathf.Ceil(style.CalcHeight(new GUIContent(value), width)) + 4;
            if (draw) GUI.Label(new Rect(x, y, width, height), value, style);
            return y + height + 8;
        }
        private float ClueBlocks(InvestigationService investigation, string topic, GUIStyle style, bool draw)
        {
            float y = 8;
            foreach (var clue in investigation.Catalog.clues)
            {
                if (clue.topicId != topic) continue;
                bool owned = investigation.HasClue(clue.clueId);
                string prefix = owned ? Loc.Get("investigation.collected_prefix") : Loc.Get("investigation.uncollected_prefix");
                y = Paragraph(8, y, 505, prefix + (investigation.CanRevealClue(clue.clueId) ? clue.Label : Loc.Get("ui.locked_tag")), style, draw);
                if (owned)
                {
                    y = Paragraph(8, y, 505, InvestigationText.Get(clue.summaryKey, Loc.Get("investigation.summary_pending")), style, draw);
                    y = Paragraph(8, y, 505, InvestigationText.Get(clue.BodyKey, InvestigationText.Get(clue.textKey,
                        Loc.Get("investigation.body_pending"))), style, draw);
                }
                y += 12;
            }
            return y;
        }
        private float ReadingBlocks(CoreReadingStage stage, bool unlocked, bool draw)
        {
            var investigation = Investigation;
            if (!unlocked) return Paragraph(12, 8, 1060, investigation.GateLabel(stage)
                + (readingAtCore ? "" : Loc.Get("investigation.new_record_hint")), textStyle, draw);
            var entry = stage.entries[readingEntry];
            float y = Paragraph(12, 8, 1060, InvestigationText.Get(entry.titleKey, Loc.Get("investigation.title_pending")), textStyle, draw);
            string body = InvestigationText.Get(entry.BodyKey, Loc.Get("investigation.body_pending"));
            // The failure summary gives away later frames. Show it after the last subtitle.
            bool still = entry.id == "record_01_failure";
            if (!still) y = Paragraph(12, y, 1060, InvestigationText.Get(entry.summaryKey, Loc.Get("investigation.record_summary_pending")), textStyle, draw);
            if (entry.id == "original_and_copy") y = ComparisonBody(y, body, draw);
            else if (still)
            {
                if (draw) DrawStill(y, stillIndex);
                string[] frames = body.Split('\f');
                y = Paragraph(12, y + 148, 1060, frames[Mathf.Min(stillIndex, frames.Length - 1)], textStyle, draw);
                if (stillIndex == 5) y = Paragraph(12, y, 1060, InvestigationText.Get(entry.summaryKey,
                    Loc.Get("investigation.record_summary_pending")), textStyle, draw);
            }
            else if (entry.id == "discarded_draft") y = DraftBody(y, body, draw);
            else y = Paragraph(12, y, 1060, body, textStyle, draw);
            string related = string.Join("\n", Array.ConvertAll(entry.relatedClues, id =>
                (investigation.HasClue(id) ? Loc.Get("investigation.collected_prefix") : Loc.Get("investigation.uncollected_prefix"))
                + (investigation.CanRevealClue(id) ? investigation.Catalog.Clue(id).Label : Loc.Get("ui.locked_tag"))));
            return Paragraph(12, y + 10, 1060, Loc.Get("investigation.related_clues", related), textStyle, draw);
        }
        private float ComparisonBody(float y, string body, bool draw)
        {
            bool stacked = Screen.width < 1280 || Screen.height < 720 || Screen.width < Screen.height * 1.45f;
            string[] rows = body.Split('\f');
            var heading = new GUIStyle(textStyle) { fontStyle = FontStyle.Bold };
            var originalChange = new GUIStyle(heading);
            var copyChange = new GUIStyle(heading);
            originalChange.normal.textColor = new Color(1f, .66f, .61f);
            copyChange.normal.textColor = new Color(.48f, .94f, .86f);
            string originalName = "", copyName = "";
            for (int i = 0; i < rows.Length; i++)
            {
                string[] fields = rows[i].Split('\t');
                if (fields.Length != 3) { y = Paragraph(12, y, 1060, rows[i], textStyle, draw); continue; }
                string original = fields[1], copy = fields[2];
                if (i == 0)
                {
                    originalName = original.Split('\n')[0];
                    copyName = copy.Split('\n')[0];
                }
                else
                {
                    y = Paragraph(12, y, 1060, fields[0], heading, draw);
                    if (stacked) { original = originalName + "\n" + original; copy = copyName + "\n" + copy; }
                }
                // The fourth comparison after the document titles is Article 3.
                bool changed = i == 4;
                float width = stacked ? 1060 : 508;
                float left = Paragraph(12, y, width, original, changed ? originalChange : textStyle, draw);
                float right = Paragraph(stacked ? 12 : 553, stacked ? left : y, width, copy, changed ? copyChange : textStyle, draw);
                y = Mathf.Max(left, right) + 18;
            }
            return y;
        }
        private float DraftBody(float y, string body, bool draw)
        {
            int start = body.IndexOf("~~", StringComparison.Ordinal);
            int end = start < 0 ? -1 : body.IndexOf("~~", start + 2, StringComparison.Ordinal);
            if (end < 0) return Paragraph(12, y, 1060, body, textStyle, draw);
            string plain = body.Remove(end, 2).Remove(start, 2);
            float bottom = Paragraph(12, y, 1060, plain, textStyle, draw);
            if (draw)
            {
                var rect = new Rect(12, y, 1060, bottom - y);
                var content = new GUIContent(plain);
                Vector2 a = textStyle.GetCursorPixelPosition(rect, content, start);
                Vector2 b = textStyle.GetCursorPixelPosition(rect, content, end - 2);
                float line = a.y + textStyle.lineHeight * .48f;
                if (b.x > a.x && Mathf.Abs(a.y - b.y) < 1)
                {
                    GUI.DrawTexture(new Rect(a.x, line, b.x - a.x, 1.5f), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(a.x, line + 3, b.x - a.x, 1.5f), Texture2D.whiteTexture);
                }
            }
            return bottom;
        }
        // Six silent archive plates: sound box, two documents, barred door, impacts, broken trace, deleted field.
        private static void DrawStill(float y, int frame)
        {
            var prior = GUI.color;
            void Block(float x, float dy, float w, float h, Color c)
            { GUI.color = c; GUI.DrawTexture(new Rect(x, y + dy, w, h), Texture2D.whiteTexture); }
            Color ink = new Color(.17f,.28f,.34f), lapis = new Color(.35f,.82f,.88f);
            Color paper = new Color(.86f,.79f,.64f), seal = new Color(.77f,.34f,.29f);
            Block(12, 0, 1060, 134, new Color(.07f,.11f,.16f));
            if (frame == 0)
            {
                Block(465,70,150,42,ink); Block(478,56,124,9,paper);
                for (int i = 0; i < 4; i++) Block(507+i*8,45-i*9,64-i*16,3,lapis);
            }
            else if (frame == 1 || frame == 5)
            {
                for (int i = 0; i < (frame == 1 ? 2 : 1); i++)
                {
                    float x = frame == 1 ? 425+125*i : 490;
                    Block(x,12,106,108,paper);
                    for (int j = 0; j < 4; j++) Block(x+12,29+j*12,74-j*5,2,ink);
                    Block(x+55,88,36,3,seal);
                    if (frame == 5) Block(x+55,95,36,3,seal);
                }
            }
            else if (frame == 2 || frame == 3)
            {
                Block(420,8,240,118,ink);
                for (int i = 0; i < 8; i++) Block(429+i*30,9,8,116,Color.gray);
                Block(476,61,133,10,lapis);
                if (frame == 3) for (int i = 0; i < 12; i++) Block(445+i*13,77+(i%3)*6,3,18,paper);
            }
            else
            {
                for (int i = 0; i < 14; i++) Block(345+i*30,22,1,88,ink);
                for (int i = 0; i < 4; i++) Block(340,28+i*24,405,1,ink);
                for (int i = 0; i < 35; i++) Block(351+i*5,65+Mathf.Sin(i*.36f)*20,6,4,lapis);
                Block(533,67,5,5,seal);
            }
            GUI.color = prior;
        }
    }
}
