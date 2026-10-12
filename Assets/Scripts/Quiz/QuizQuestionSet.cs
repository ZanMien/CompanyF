using System;
using System.Collections.Generic;
using UnityEngine;

namespace CompanyF.Quiz
{
    /// <summary>Questions asked at the junctions, in order: junction N asks entry N-1. Edit in the Inspector.</summary>
    [CreateAssetMenu(menuName = "CompanyF/Quiz Question Set", fileName = "QuizQuestions")]
    public class QuizQuestionSet : ScriptableObject
    {
        [Serializable]
        public class Question
        {
            [TextArea(1, 3)] public string text;
            [Tooltip("Left option (P1: A / P2: ←).")] public string left;
            [Tooltip("Right option (P1: D / P2: →).")] public string right;
        }

        public List<Question> questions = new List<Question>();

        public Question Get(int index) => index >= 0 && index < questions.Count ? questions[index] : null;
    }
}
