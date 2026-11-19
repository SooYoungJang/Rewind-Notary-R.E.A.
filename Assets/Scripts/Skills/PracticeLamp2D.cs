using System.Collections.Generic;
using UnityEngine;

namespace RewindNotary
{
    public sealed class PracticeLamp2D : MonoBehaviour
    {
        public static readonly List<PracticeLamp2D> All = new List<PracticeLamp2D>();
        public Bounds Bounds => SkillGeometry.Area(transform.position, SkillGeometry.LampRadius);
        private bool spent;
        private Color readyColor = Color.cyan, spentColor = Color.gray;
        private SpriteRenderer visual;
        private void Awake()
        {
            visual = gameObject.AddComponent<SpriteRenderer>(); visual.sprite = RoomArt.Station("opera"); visual.sortingOrder = 5;
            transform.localScale = Vector3.one * SkillGeometry.LampRadius * 2; ResetAttempt();
        }
        internal void SetPicture(Sprite picture)
        {
            visual.sprite = picture;
            readyColor = new Color(.51f,.63f,.78f);
            spentColor = new Color(1f,.88f,.59f);
            visual.color = spent ? spentColor : readyColor;
        }
        private void OnEnable() { All.Add(this); }
        private void OnDisable() { All.Remove(this); }
        public bool Trigger()
        { if (spent) return false; spent = true; visual.color = spentColor; return true; }
        public void ResetAttempt() { spent = false; visual.color = readyColor; }
    }
}
