﻿using System.Collections.Generic;
using System.Linq;
using SimpleJSON;

namespace Beatmap.Info
{
    public static class V2Info
    {
        public static BaseInfo GetFromJson(JSONNode node)
        {
            var info = new BaseInfo();

            info.Version = node["_version"].Value;
            info.SongName = node["_songName"].Value;
            info.SongSubName = node["_songSubName"].Value;
            info.SongAuthorName = node["_songAuthorName"].Value;
            
            info.LevelAuthorName = node["_levelAuthorName"].Value;
            
            info.BeatsPerMinute = node["_beatsPerMinute"].AsFloat;
            
            // Deprecated fields
            info.SongTimeOffset = node["_songTimeOffset"].AsFloat;
            info.Shuffle = node["_shuffle"].AsFloat;
            info.ShufflePeriod = node["_shufflePeriod"].AsFloat;

            info.PreviewStartTime = node["_previewStartTime"].AsFloat;
            info.PreviewDuration = node["_previewDuration"].AsFloat;

            info.SongFilename = node["_songFilename"].Value;
            info.CoverImageFilename = node["_coverImageFilename"].Value;

            info.EnvironmentName = node["_environmentName"].Value;
            info.EnvironmentNames = node["_environmentNames"].AsArray.Children.Select(x => x.Value).ToList();
            info.AllDirectionsEnvironmentName = node["_allDirectionsEnvironmentName"].Value;

            var colorSchemes = new List<InfoColorScheme>();
            var colorSchemeNodes = node["_colorSchemes"].AsArray.Children.Select(x => x.AsObject);
            foreach (var colorSchemeNode in colorSchemeNodes)
            {
                var colorScheme = new InfoColorScheme();
                colorScheme.UseOverride = colorSchemeNode["useOverride"].AsBool;
                colorScheme.ColorSchemeName = colorSchemeNode["colorScheme"]["colorSchemeId"];
                colorScheme.SaberAColor = colorSchemeNode["colorScheme"]["saberAColor"].ReadColor();
                colorScheme.SaberBColor = colorSchemeNode["colorScheme"]["saberBColor"].ReadColor();
                colorScheme.ObstaclesColor = colorSchemeNode["colorScheme"]["obstaclesColor"].ReadColor();
                colorScheme.EnvironmentColor0 = colorSchemeNode["colorScheme"]["environmentColor0"].ReadColor();
                colorScheme.EnvironmentColor1 = colorSchemeNode["colorScheme"]["environmentColor1"].ReadColor();
                colorScheme.EnvironmentColor0Boost = colorSchemeNode["colorScheme"]["environmentColor0Boost"].ReadColor();
                colorScheme.EnvironmentColor1Boost = colorSchemeNode["colorScheme"]["environmentColor1Boost"].ReadColor();
                colorSchemes.Add(colorScheme);
            }
            info.ColorSchemes = colorSchemes;

            var beatmapSets = new List<InfoDifficultySet>();
            var beatmapSetsNode = node["_difficultyBeatmapSets"].AsArray;
            foreach (var beatmapSetNode in beatmapSetsNode.Children)
            {
                var beatmapSet = new InfoDifficultySet();
                beatmapSet.Characteristic = beatmapSetNode["_beatmapCharacteristicName"].Value;
                
                var difficulties = new List<InfoDifficulty>();

                var beatmapsNode = beatmapSetNode["_difficultyBeatmaps"].AsArray;
                foreach (var beatmapNode in beatmapsNode.Children)
                {
                    var infoDifficulty = new InfoDifficulty(beatmapSet);
                    infoDifficulty.BeatmapFileName = beatmapNode["_beatmapFilename"].Value;
                    infoDifficulty.Difficulty = beatmapNode["_difficulty"].Value;
                    infoDifficulty.EnvironmentNameIndex = beatmapNode["_environmentNameIdx"].AsInt;
                    infoDifficulty.ColorSchemeIndex = beatmapNode["_beatmapColorSchemeIdx"].AsInt;
                    infoDifficulty.NoteJumpSpeed = beatmapNode["_noteJumpMovementSpeed"].AsFloat;
                    infoDifficulty.NoteStartBeatOffset = beatmapNode["_noteJumpStartBeatOffset"].AsFloat;

                    infoDifficulty.CustomData = beatmapNode["_customData"].AsObject;
                    
                    difficulties.Add(infoDifficulty);
                }

                beatmapSet.Difficulties = difficulties;
                beatmapSet.CustomData = beatmapSetNode["_customData"].AsObject;

                beatmapSets.Add(beatmapSet);
            }
            info.DifficultySets = beatmapSets;
            
            info.CustomData = node["_customData"];
            
            return info;
        }

        public static JSONNode GetOutputJson(BaseInfo info)
        {
            var json = new JSONObject();

            json["_version"] = "2.1.0";

            json["_songName"] = info.SongName;
            json["_songSubName"] = info.SongSubName;
            json["_songSubName"] = info.SongSubName;
            json["_songAuthorName"] = info.SongAuthorName;
            json["_levelAuthorName"] = info.LevelAuthorName;
            json["_beatsPerMinute"] = info.BeatsPerMinute;
            json["_songTimeOffset"] = info.SongTimeOffset;
            json["_shuffle"] = info.Shuffle;
            json["_shufflePeriod"] = info.ShufflePeriod;
            json["_previewStartTime"] = info.PreviewStartTime;
            json["_previewDuration"] = info.PreviewDuration;
            json["_songFilename"] = info.SongFilename;
            json["_coverImageFilename"] = info.CoverImageFilename;
            json["_environmentName"] = info.EnvironmentName;
            json["_allDirectionsEnvironmentName"] = info.AllDirectionsEnvironmentName;

            var environmentNames = new JSONArray();
            foreach (var environmentName in info.EnvironmentNames)
            {
                environmentNames.Add(environmentName);
            }
            json["_environmentNames"] = environmentNames;

            var colorSchemes = new JSONArray();
            foreach (var colorScheme in info.ColorSchemes)
            {
                var node = new JSONObject();
                node["useOverride"] = colorScheme.UseOverride;
                node["colorScheme"]["colorSchemeId"] = colorScheme.ColorSchemeName;
                node["colorScheme"]["saberAColor"] = colorScheme.SaberAColor;
                node["colorScheme"]["saberBColor"] = colorScheme.SaberBColor;
                node["colorScheme"]["obstaclesColor"] = colorScheme.ObstaclesColor;
                node["colorScheme"]["environmentColor0"] = colorScheme.EnvironmentColor0;
                node["colorScheme"]["environmentColor1"] = colorScheme.EnvironmentColor1;
                node["colorScheme"]["environmentColor0Boost"] = colorScheme.EnvironmentColor0Boost;
                node["colorScheme"]["environmentColor1Boost"] = colorScheme.EnvironmentColor1Boost;
                
                colorSchemes.Add(node);
            }
            json["_colorSchemes"] = colorSchemes;

            var beatmapSetArray = new JSONArray();
            foreach (var beatmapSet in info.DifficultySets)
            {
                var setNode = new JSONObject { ["_beatmapCharacteristicName"] = beatmapSet.Characteristic };
                var difficultyBeatmapsArray = new JSONArray();

                foreach (var difficulty in beatmapSet.Difficulties)
                {
                    var node = new JSONObject();
                    node["_difficulty"] = difficulty.Difficulty;
                    node["_difficultyRank"] = difficulty.DifficultyRank;
                    node["_beatmapFilename"] = difficulty.BeatmapFileName;
                    node["_noteJumpMovementSpeed"] = difficulty.NoteJumpSpeed;
                    node["_noteJumpStartBeatOffset"] = difficulty.NoteStartBeatOffset;
                    node["_beatmapColorSchemeIdx"] = difficulty.ColorSchemeIndex;
                    node["_environmentNameIdx"] = difficulty.EnvironmentNameIndex;
                    node["_customData"] = difficulty.CustomData;
                    difficultyBeatmapsArray.Add(node);
                }

                setNode["_difficultyBeatmaps"] = difficultyBeatmapsArray;
                setNode["_customData"] = beatmapSet.CustomData;
                beatmapSetArray.Add(setNode);
            }

            json["_difficultyBeatmapSets"] = beatmapSetArray;

            json["_customData"] = info.CustomData;
            
            return json;
        }
    }
}
