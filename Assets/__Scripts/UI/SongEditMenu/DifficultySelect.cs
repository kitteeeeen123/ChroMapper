using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Beatmap.Base;
using Beatmap.Info;
using Beatmap.V2;
using Beatmap.V3;
using SimpleJSON;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static BeatSaberSong;

public class DifficultySelect : MonoBehaviour
{
    [SerializeField] private TMP_InputField njsField;
    [SerializeField] private TMP_InputField songBeatOffsetField;
    [SerializeField] private CharacteristicSelect characteristicSelect;
    [SerializeField] private Color copyColor;
    [SerializeField] private EnvRemoval envRemoval;
    [SerializeField] private Button openEditorButton;

    private readonly Dictionary<string, int> diffRankLookup = new Dictionary<string, int>
    {
        {"Easy", 1},
        {"Normal", 3},
        {"Hard", 5},
        {"Expert", 7},
        {"ExpertPlus", 9}
    };

    private readonly HashSet<DifficultyRow> rows = new HashSet<DifficultyRow>();
    private readonly Dictionary<string, string> selectedMemory = new Dictionary<string, string>();
    public Dictionary<string, Dictionary<string, DifficultySettings>> Characteristics;
    private CopySource copySource;
    [Obsolete("Use InfoDifficultySet", true)]
    private DifficultyBeatmapSet currentCharacteristic;
    private Dictionary<string, DifficultySettings> diffs;

    private InfoDifficultySet currentDifficultySet;
    
    private bool loading;
    private DifficultyRow selected;

    public BaseDifficulty CurrentDiff => selected != null ? diffs[selected.Name].Map : null;

    [Obsolete("Use MapInfo",true)]
    private BeatSaberSong Song => BeatSaberSongContainer.Instance != null ? BeatSaberSongContainer.Instance.Song : null;

    private BaseInfo MapInfo => BeatSaberSongContainer.Instance != null ? BeatSaberSongContainer.Instance.Info : null;
    

    /// <summary>
    ///     Load song data and set up listeners on UI elements
    /// </summary>
    public void Start()
    {
        if (MapInfo?.DifficultySets != null)
        {
            Characteristics = MapInfo.DifficultySets.GroupBy(it => it.Characteristic).ToDictionary(
                characteristic => characteristic.Key,
                characteristic => characteristic.SelectMany(i => i.Difficulties).GroupBy(map => map.Difficulty)
                    .ToDictionary(
                        grouped => grouped.Key,
                        grouped => new DifficultySettings(grouped.First())
                    ),
                StringComparer.OrdinalIgnoreCase
            );
        }
        else
        {
            Characteristics = new Dictionary<string, Dictionary<string, DifficultySettings>>();
        }

        foreach (Transform child in transform)
        {
            // Add event listeners, it's hard to do this staticly as the rows are prefabs
            // so they can't access the parent object with this script
            var row = new DifficultyRow(child);
            rows.Add(row);

            row.Toggle.onValueChanged.AddListener(val => OnChange(row, val));
            row.Button.onClick.AddListener(() => OnClick(row));
            row.NameInput.onValueChanged.AddListener(name => OnValueChanged(row, name));
            row.Copy.onClick.AddListener(() => SetCopySource(row));
            row.Save.onClick.AddListener(() => SaveDiff(row));
            row.Revert.onClick.AddListener(() => Revertdiff(row));
            row.Paste.onClick.AddListener(() => DoPaste(row));
        }
    }

    /// <summary>
    ///     Update the offset of the selected diff
    ///     If there's no selected diff this just goes into oblivion
    /// </summary>
    public void UpdateOffset()
    {
        if (selected == null || !diffs.ContainsKey(selected.Name)) return;

        var diff = diffs[selected.Name];

        if (float.TryParse(songBeatOffsetField.text, out var temp2)) diff.NoteJumpStartBeatOffset = temp2;

        selected.ShowDirtyObjects(diff);
    }

    /// <summary>
    ///     Update the NJS of the selected diff
    ///     If there's no selected diff this just goes into oblivion
    /// </summary>
    public void UpdateNJS()
    {
        if (selected == null || !diffs.ContainsKey(selected.Name)) return;

        var diff = diffs[selected.Name];

        if (float.TryParse(njsField.text, out var temp)) diff.NoteJumpMovementSpeed = temp;

        selected.ShowDirtyObjects(diff);
    }

    public void UpdateEnvRemoval()
    {
        if (selected == null || !diffs.ContainsKey(selected.Name)) return;

        var diff = diffs[selected.Name];

        diff.EnvEnhancements = envRemoval.EnvRemovalList;

        selected.ShowDirtyObjects(diff);
    }

    /// <summary>
    ///     Revert the diff to the saved version
    /// </summary>
    /// <param name="obj">UI row that was clicked on</param>
    private void Revertdiff(DifficultyRow row)
    {
        var localDiff = diffs[row.Name];
        localDiff.Revert();

        row.NameInput.text = localDiff.CustomName;

        if (row == selected)
        {
            njsField.text = localDiff.NoteJumpMovementSpeed.ToString();
            songBeatOffsetField.text = localDiff.NoteJumpStartBeatOffset.ToString();
            envRemoval.UpdateFromDiff(localDiff.EnvEnhancements);
        }

        row.ShowDirtyObjects(localDiff);
    }

    private BaseDifficulty TryGetExistingMapFromDiff(DifficultySettings diff)
    {
        try
        {
            return diff.Map;
        }
        catch (Exception) { }

        ;

        return null;
    }

    public void SaveAllDiffs()
    {
        foreach (var row in rows)
        {
            if (diffs.ContainsKey(row.Name))
                SaveDiff(row);
        }
    }

    /// <summary>
    ///     Save the diff
    /// </summary>
    /// <param name="row">UI row that was clicked on</param>
    private void SaveDiff(DifficultyRow row)
    {
        var mapInfo = BeatSaberSongContainer.Instance.Info;
        if (!Directory.Exists(mapInfo.Directory))
            mapInfo.Save();

        var localDiff = diffs[row.Name];
        var firstSave = localDiff.ForceDirty;
        localDiff.Commit();
        row.ShowDirtyObjects(false, true);

        var diff = localDiff.InfoDifficulty;

        if (!mapInfo.DifficultySets.Contains(currentDifficultySet))
            mapInfo.DifficultySets.Add(currentDifficultySet);
        if (!currentDifficultySet.Difficulties.Contains(diff))
            currentDifficultySet.Difficulties.Add(diff);

        var map = TryGetExistingMapFromDiff(localDiff) ?? new BaseDifficulty();
        var oldPath = map.DirectoryAndFile;

        diff.SetBeatmapFileNameToDefault();
        map.DirectoryAndFile = Path.Combine(mapInfo.Directory, diff.BeatmapFileName);
        if (File.Exists(oldPath) && oldPath != map.DirectoryAndFile && !File.Exists(map.DirectoryAndFile))
        {
            if (firstSave)
                File.Copy(oldPath, map.DirectoryAndFile);
            else
                File.Move(oldPath, map.DirectoryAndFile); //This should properly "convert" difficulties just fine
        }
        else
        {
            map.Save();
        }

        // diff.RefreshRequirementsAndWarnings(map);

        mapInfo.Save();
        characteristicSelect.Recalculate();

        Debug.Log("Saved " + row.Name);
    }

    /// <summary>
    ///     Handle changes to the difficulty label
    /// </summary>
    /// <param name="row">UI row that was updated</param>
    /// <param name="difficultyLabel">New label value</param>
    private void OnValueChanged(DifficultyRow row, string difficultyLabel)
    {
        if (!diffs.ContainsKey(row.Name)) return;

        var diff = diffs[row.Name];

        // Expert+ is special as the only difficulty that is different in JSON
        var defaultName = row.Name == "ExpertPlus" ? "Expert+" : row.Name;
        if (difficultyLabel != "" && difficultyLabel != defaultName)
            diff.CustomName = difficultyLabel;
        else
            diff.CustomName = null;

        row.ShowDirtyObjects(diff);
    }

    /// <summary>
    ///     Helper to deselect the currently selected row
    /// </summary>
    private void DeselectDiff()
    {
        if (selected != null)
        {
            var selImage = selected.Background;
            selImage.color = new Color(selImage.color.r, selImage.color.g, selImage.color.b, 0.0f);

            // Clean the UI, if we're selecting a new item they'll be repopulated
            BeatSaberSongContainer.Instance.MapDifficultyInfo = null;
            njsField.text = "";
            songBeatOffsetField.text = "";
            envRemoval.ClearList();
        }

        selected = null;
        openEditorButton.interactable = false;
    }

    /// <summary>
    ///     Helper for ForwardOnClick which handles clicks on the difficulty label text
    /// </summary>
    /// <param name="obj">UI row that was clicked on</param>
    public void OnClick(Transform obj)
    {
        var row = rows.First(it => it.Obj == obj);
        if (row != null) OnClick(row);
    }

    /// <summary>
    ///     Handle selecting the row when clicked
    /// </summary>
    /// <param name="row">UI row that was clicked on</param>
    private void OnClick(DifficultyRow row)
    {
        if (!diffs.ContainsKey(row.Name)) return;

        DeselectDiff();

        // Select a difficulty
        selected = row;
        openEditorButton.interactable = true;
        if (!loading) selectedMemory[currentDifficultySet.Characteristic] = selected.Name;
        var selImage = selected.Background;
        selImage.color = new Color(selImage.color.r, selImage.color.g, selImage.color.b, 1.0f);

        var selectedDifficultySettings = diffs[row.Name];
        BeatSaberSongContainer.Instance.MapDifficultyInfo = selectedDifficultySettings.InfoDifficulty;

        njsField.text = selectedDifficultySettings.NoteJumpMovementSpeed.ToString();
        songBeatOffsetField.text = selectedDifficultySettings.NoteJumpStartBeatOffset.ToString();
        envRemoval.UpdateFromDiff(selectedDifficultySettings.EnvEnhancements);
    }

    /// <summary>
    ///     Paste from another difficulty to this one
    ///     As the toggles are hidden in this mode and replaced with paste icons
    ///     we just forward the click to the toggle below
    /// </summary>
    /// <param name="row">UI row that was clicked on</param>
    private void DoPaste(DifficultyRow row) =>
        // This will trigger the code in OnChange below
        row.Toggle.isOn = true;

    /// <summary>
    ///     Handle adding and deleting difficulties, they aren't added to the
    ///     song being edited until they are saved so this method stages them
    /// </summary>
    /// <param name="row">UI row that was clicked on</param>
    /// <param name="val">True if the diff is being added</param>
    private void OnChange(DifficultyRow row, bool val)
    {
        if (!val && diffs.ContainsKey(row.Name)) // Delete if exists
        {
            // ForceDirty = has never been saved, don't ask for permission
            if (diffs[row.Name].ForceDirty)
            {
                if (row == selected) DeselectDiff();

                diffs.Remove(row.Name);
                row.SetInteractable(false);
                row.NameInput.text = "";
                row.ShowDirtyObjects(false, false);
                return;
            }

            // This diff has previously been saved, confirm deletion
            PersistentUI.Instance.ShowDialogBox("SongEditMenu", "deletediff.dialog",
                r => HandleDeleteDifficulty(row, r), PersistentUI.DialogBoxPresetType.YesNo,
                new object[] { diffs[row.Name].InfoDifficulty.Difficulty });
        }
        else if (val && !diffs.ContainsKey(row.Name)) // Create if does not exist
        {
            var map = new InfoDifficulty(currentDifficultySet) { Difficulty = row.Name };

            map.SetBeatmapFileNameToDefault();

            if (copySource != null)
            {
                var fromDiff = copySource.DifficultySettings;

                CancelCopy();

                if (fromDiff != null)
                {
                    map.NoteJumpSpeed = fromDiff.InfoDifficulty.NoteJumpSpeed;
                    map.NoteStartBeatOffset = fromDiff.InfoDifficulty.NoteStartBeatOffset;

                    map.CustomData = fromDiff.InfoDifficulty.CustomData?.Clone().AsObject;

                    // Yes this copies custom data, but color overrides dont copy since they're ripped from these fields instead.
                    map.CustomColorLeft = fromDiff.InfoDifficulty.CustomColorLeft;
                    map.CustomColorRight = fromDiff.InfoDifficulty.CustomColorRight;
                    map.CustomEnvColorLeft = fromDiff.InfoDifficulty.CustomEnvColorLeft;
                    map.CustomEnvColorRight = fromDiff.InfoDifficulty.CustomEnvColorRight;
                    map.CustomEnvColorWhite = fromDiff.InfoDifficulty.CustomEnvColorWhite;
                    map.CustomColorObstacle = fromDiff.InfoDifficulty.CustomColorObstacle;
                    map.CustomEnvColorBoostLeft = fromDiff.InfoDifficulty.CustomEnvColorBoostLeft;
                    map.CustomEnvColorBoostRight = fromDiff.InfoDifficulty.CustomEnvColorBoostRight;
                    map.CustomEnvColorBoostWhite = fromDiff.InfoDifficulty.CustomEnvColorBoostWhite;

                    // This sets the current filename as the filename for another diff and will trigger the copy on save
                    map.BeatmapFileName = fromDiff.InfoDifficulty.BeatmapFileName;
                }
            }

            diffs[row.Name] = new DifficultySettings(map, true);

            if (!string.IsNullOrEmpty(diffs[row.Name].CustomName)) diffs[row.Name].CustomName += " (Copy)";

            row.NameInput.text = diffs[row.Name].CustomName;
            row.ShowDirtyObjects(diffs[row.Name]);
            row.SetInteractable(true);
            OnClick(row);
        }
        else if (val) // Create, but already exists
        {
            // I don't know how this would happen anymore
            row.ShowDirtyObjects(diffs[row.Name]);
            row.SetInteractable(true);
            if (!loading) OnClick(row);
        }
    }

    /// <summary>
    ///     Handle deleting a difficulty that was previously saved
    /// </summary>
    /// <param name="row">UI row that was clicked on</param>
    /// <param name="r">Confirmation from the user</param>
    private void HandleDeleteDifficulty(DifficultyRow row, int r)
    {
        if (r == 1) // User canceled out
        {
            row.Toggle.isOn = true;
            return;
        }

        var diff = diffs[row.Name].InfoDifficulty;

        var fileToDelete = Path.Combine(MapInfo.Directory, diff.BeatmapFileName);// BeatSaberSong.GetMapFromDifficultyBeatmap(diff)?.DirectoryAndFile;
        if (File.Exists(fileToDelete)) FileOperationAPIWrapper.MoveToRecycleBin(fileToDelete);

        // Remove status effects if present
        if (copySource != null && row == copySource.Obj &&
            currentDifficultySet == copySource.CharacteristicSet)
        {
            CancelCopy();
        }

        if (row == selected) DeselectDiff();

        currentDifficultySet.Difficulties.Remove(diffs[row.Name].InfoDifficulty);
        if (currentDifficultySet.Difficulties.Count == 0)
            MapInfo.DifficultySets.Remove(currentDifficultySet);

        diffs.Remove(row.Name);
        MapInfo.Save();

        row.SetInteractable(false);
        row.NameInput.text = "";
        row.ShowDirtyObjects(false, false);
        characteristicSelect.Recalculate();
    }

    /// <summary>
    ///     Set the row as the source for a copy-paste operation
    /// </summary>
    /// <param name="row">UI row that was clicked on</param>
    private void SetCopySource(DifficultyRow row)
    {
        // If we copied from the current characteristic remove the highlight
        if (copySource != null && currentDifficultySet == copySource.CharacteristicSet)
            copySource.Obj.CopyImage.color = Color.white;

        // Clicking twice on the same source removes it
        if (copySource != null && copySource.Obj == row && currentDifficultySet == copySource.CharacteristicSet)
        {
            CancelCopy();
            return;
        }

        copySource = new CopySource(diffs[row.Name], currentDifficultySet, row);
        SetPasteMode(true);
        row.CopyImage.color = copyColor;
    }

    /// <summary>
    ///     Helper to clear any in progress copy-paste
    /// </summary>
    public void CancelCopy()
    {
        if (copySource != null && currentDifficultySet == copySource.CharacteristicSet)
            copySource.Obj.CopyImage.color = Color.white;
        copySource = null;
        SetPasteMode(false);
    }

    /// <summary>
    ///     Show the difficulties for the given characteristic
    /// </summary>
    /// <param name="name">Characteristic to load from</param>
    public void SetCharacteristic(string name, bool firstLoad = false)
    {
        DeselectDiff();

        currentDifficultySet = MapInfo?.DifficultySets?.Find(it =>
            it.Characteristic.Equals(name, StringComparison.InvariantCultureIgnoreCase));
        if (currentDifficultySet == null)
            // Create a new set locally if the song doesn't have one,
            // will only be written back if a difficulty is created
            currentDifficultySet = new InfoDifficultySet { Characteristic = name };

        if (!Characteristics.ContainsKey(name)) Characteristics.Add(name, new Dictionary<string, DifficultySettings>());
        diffs = Characteristics[name];

        loading = true;
        selectedMemory.TryGetValue(name, out var prevDiffLog);
        foreach (var row in rows)
        {
            var hasDiff = diffs.ContainsKey(row.Name);
            row.SetInteractable(diffs.ContainsKey(row.Name));
            // Highlight the copy source if it's here
            row.CopyImage.color =
                copySource != null && currentDifficultySet == copySource.CharacteristicSet && copySource.Obj == row
                    ? copyColor
                    : Color.white;

            row.NameInput.text = hasDiff ? diffs[row.Name].CustomName : "";

            if (hasDiff)
            {
                row.ShowDirtyObjects(diffs[row.Name]);
                if (firstLoad && Settings.Instance.LastLoadedMap.Equals(MapInfo?.Directory) &&
                    Settings.Instance.LastLoadedDiff.Equals(row.Name))
                {
                    selectedMemory[name] = row.Name;
                    OnClick(row);
                }
                else if (selected == null || (!firstLoad && selectedMemory.TryGetValue(name, out var prevDiff) &&
                                              row.Name.Equals(prevDiff)))
                {
                    OnClick(row);
                }
            }
            else
            {
                row.ShowDirtyObjects(false, false);
            }
        }

        loading = false;

        SetPasteMode(copySource != null);

        if (selected == null)
        {
            njsField.text = "";
            songBeatOffsetField.text = "";
            envRemoval.ClearList();
        }
    }

    /// <summary>
    ///     Show or hide paste buttons for non-existing difficulties
    /// </summary>
    /// <param name="mode">True if we should show paste buttons</param>
    private void SetPasteMode(bool mode)
    {
        foreach (Transform child in transform)
        {
            var localMode = mode && !diffs.ContainsKey(child.name);
            child.Find("Paste").gameObject.SetActive(localMode);
            child.Find("Button/Toggle").gameObject.SetActive(!localMode);
        }
    }

    /// <summary>
    ///     Check if any difficulties have unsaved changes
    /// </summary>
    /// <returns>True if there are unsaved changes</returns>
    public bool IsDirty() => Characteristics.Any(it => it.Value.Any(diff => diff.Value.IsDirty()));
}
