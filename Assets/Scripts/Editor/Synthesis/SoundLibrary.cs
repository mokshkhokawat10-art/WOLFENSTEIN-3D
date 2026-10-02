using System;
using System.Collections.Generic;

namespace Wolfenstein.Editor.Synthesis
{
    /// <summary>
    /// Every clip the game's sounds are built from, each made by a recipe from noise, oscillators, voices and resonant bodies.
    /// Nothing is recorded or downloaded. The recipes are grouped by what they are for, one file per group.
    /// </summary>
    internal static partial class SoundLibrary
    {
        private static List<SoundRecipe> all;

        public static IReadOnlyList<SoundRecipe> All => all ??= Create();

        /// <summary>The paths of a clip and its numbered variations, such as "Weapons/PistolShot_1" to "_3".</summary>
        public static List<string> PathsOf(string path)
        {
            var paths = new List<string>();
            foreach (SoundRecipe recipe in All)
            {
                if (recipe.Path == path || (recipe.Path.StartsWith(path + "_", StringComparison.Ordinal) &&
                                            int.TryParse(recipe.Path.Substring(path.Length + 1), out _)))
                    paths.Add(recipe.Path);
            }

            return paths;
        }

        private static List<SoundRecipe> Create()
        {
            var recipes = new List<SoundRecipe>();
            AddWeapons(recipes);
            AddPlayer(recipes);
            AddCreatures(recipes);
            AddDoors(recipes);
            AddEvents(recipes);
            AddAmbience(recipes);
            AddMusic(recipes);
            AddProgression(recipes);
            return recipes;
        }

        private static void Add(List<SoundRecipe> recipes, string path, SoundKind kind, Func<Studio, Sound> build) =>
            recipes.Add(new SoundRecipe(path, kind, build));

        private static void Add(List<SoundRecipe> recipes, string path, Func<Studio, Sound> build) =>
            recipes.Add(new SoundRecipe(path, SoundKind.Effect, build));

        /// <summary>Numbered variations of one effect, each told which it is, so a cue can pick among them.</summary>
        private static void Variants(List<SoundRecipe> recipes, string path, int count, Func<Studio, int, Sound> build)
        {
            for (int i = 1; i <= count; i++)
            {
                int variant = i;
                recipes.Add(new SoundRecipe($"{path}_{variant}", SoundKind.Effect, studio => build(studio, variant)));
            }
        }
    }
}
