using UnityEngine;
using UnityEngine.SceneManagement;

namespace Wolfenstein.Progression
{
    /// <summary>
    /// What carries from one level to the next: the score. Loading a level through the session hands it the score to
    /// start from, and only that level can claim it, so a scene loaded any other way, such as by a test, starts from nothing.
    /// Finishing the episode returns to the title with the final score. Nothing is saved to disk.
    /// </summary>
    public static class GameSession
    {
        private static string pendingScene;
        private static int pendingScore;
        private static bool episodeFinished;
        private static int finalScore;

        /// <summary>Loads a level, which starts from the given score.</summary>
        public static void LoadLevel(string sceneName, int carriedScore)
        {
            pendingScene = sceneName;
            pendingScore = Mathf.Max(0, carriedScore);
            SceneManager.LoadScene(sceneName);
        }

        /// <summary>The score a level was loaded with, once, or 0 if it was not loaded through the session.</summary>
        public static int ClaimCarriedScore(string sceneName)
        {
            int score = pendingScene == sceneName ? pendingScore : 0;
            pendingScene = null;
            pendingScore = 0;
            return score;
        }

        /// <summary>Ends the episode after its last level, and returns to the title screen.</summary>
        public static void FinishEpisode(int score, string titleScene)
        {
            pendingScene = null;
            pendingScore = 0;
            episodeFinished = true;
            finalScore = score;
            SceneManager.LoadScene(titleScene);
        }

        /// <summary>Whether the episode has just been finished, and its final score. Answers true only once.</summary>
        public static bool ClaimFinishedEpisode(out int score)
        {
            score = finalScore;
            bool finished = episodeFinished;
            episodeFinished = false;
            finalScore = 0;
            return finished;
        }

        // Static state survives between play sessions in the editor when domain reload is off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            pendingScene = null;
            pendingScore = 0;
            episodeFinished = false;
            finalScore = 0;
        }
    }
}
