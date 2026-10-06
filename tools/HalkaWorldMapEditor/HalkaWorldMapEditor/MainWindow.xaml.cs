using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using HalkaWorldMapEditor.Core;
using Microsoft.Win32;
using SkiaSharp;
using SkiaSharp.Views.Desktop;

namespace HalkaWorldMapEditor;

public partial class MainWindow : Window
{
    private static readonly SKTypeface MarkerTypeface = SKTypeface.FromFamilyName("Yu Gothic UI") ?? SKTypeface.Default;
    private enum EditorTool { Select, Paint, Erase }
    private sealed record PaletteEntry(string DefinitionId, string DisplayName, bool IsSurface,
        bool RestoresBase, BitmapImage? PreviewIcon, string Tooltip, string Category = "オブジェクト", bool IsEntity = false)
    {
        public string GroupName => IsSurface ? "地面" : Category;
        public string AssetStatus => !IsSurface && PreviewIcon == null ? "MISSING ASSET" : "";
    }
    private sealed record MapChoice(string Path, string MapId, string DisplayName);
    private sealed class LocalSettings
    {
        public string ProjectRoot { get; set; } = "";
        public string MapFile { get; set; } = "";
        public double Zoom { get; set; } = 32;
        public double PanX { get; set; }
        public double PanY { get; set; }
        public bool ShowGrid { get; set; } = true;
        public bool ShowGrass { get; set; } = true;
        public bool ShowCollision { get; set; }
        public bool ShowCoordinates { get; set; }
        public bool ShowMarkers { get; set; } = true;
        public bool ShowActionPoints { get; set; } = true;
        public bool ShowEntities { get; set; } = true;
    }

    private readonly Dictionary<string, SKBitmap?> spriteCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly string settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HALKA WORLD MAP EDITOR", "settings.json");
    private LocalSettings settings = new();
    private MapSession? session;
    private CatalogDocument? catalog;
    private string? projectRoot;
    private EditorTool tool = EditorTool.Select;
    private string? selectedInstanceId;
    private GridCell? selectedCell;
    private DateTime knownMapWriteTime;
    private bool externalChangePending;
    private bool dragPaint;
    private GridCell? lastPaintCell;
    private bool dragPan;
    private bool rightErase;
    private Point lastPointer;
    private bool loading;
    private double zoom = 32;
    private double panX;
    private double panY;

    public MainWindow() => InitializeComponent();

    private void WindowLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            if (File.Exists(settingsPath)) settings = JsonSerializer.Deserialize<LocalSettings>(File.ReadAllText(settingsPath)) ?? new();
            zoom = Math.Clamp(settings.Zoom, 8, 160);
            panX = settings.PanX; panY = settings.PanY;
            GridCheck.IsChecked = settings.ShowGrid; GrassCheck.IsChecked = settings.ShowGrass;
            CollisionCheck.IsChecked = settings.ShowCollision; CoordCheck.IsChecked = settings.ShowCoordinates;
            MarkerCheck.IsChecked = settings.ShowMarkers;
            ActionPointCheck.IsChecked = settings.ShowActionPoints;
            EntityCheck.IsChecked = settings.ShowEntities;
            var found = ProjectPaths.IsUnityProject(settings.ProjectRoot) ? settings.ProjectRoot :
                ProjectPaths.FindNear(AppContext.BaseDirectory) ?? ProjectPaths.FindNear(Environment.CurrentDirectory);
            if (found != null) OpenProject(found, settings.MapFile, fit: settings.MapFile.Length == 0);
            else SelectProjectFolder();
        }
        catch (Exception error) { ShowError("起動", error); }
    }

    private void WindowActivated(object? sender, EventArgs e)
    {
        if (session == null || !File.Exists(session.FilePath)) return;
        var current = File.GetLastWriteTimeUtc(session.FilePath);
        if (current <= knownMapWriteTime) return;
        if (session.IsDirty)
        {
            externalChangePending = true;
            knownMapWriteTime = current;
            StatusText.Text = "外部でMapが更新されました。未保存の編集があるため自動再読込しません。";
            MessageBox.Show(this, "Mapファイルが外部で変更されました。未保存の編集を保持しています。保存または再読込を選んでください。", "外部変更", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        else try { LoadMap(session.FilePath, fit: false); }
            catch (Exception error) { ShowError("外部更新の再読み込み", error); }
    }

    private void WindowClosing(object? sender, CancelEventArgs e)
    {
        if (!ConfirmDiscard()) { e.Cancel = true; return; }
        SaveSettings();
        foreach (var image in spriteCache.Values) image?.Dispose();
    }

    private void SaveSettings()
    {
        settings.ProjectRoot = projectRoot ?? "";
        settings.MapFile = session?.FilePath ?? "";
        settings.Zoom = zoom; settings.PanX = panX; settings.PanY = panY;
        settings.ShowGrid = GridCheck.IsChecked == true; settings.ShowGrass = GrassCheck.IsChecked == true;
        settings.ShowCollision = CollisionCheck.IsChecked == true; settings.ShowCoordinates = CoordCheck.IsChecked == true;
        settings.ShowMarkers = MarkerCheck.IsChecked == true;
        settings.ShowActionPoints = ActionPointCheck.IsChecked == true;
        settings.ShowEntities = EntityCheck.IsChecked == true;
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        File.WriteAllText(settingsPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
    }

    private bool ConfirmDiscard() => session?.IsDirty != true ||
        MessageBox.Show(this, "未保存の変更があります。破棄して続けますか？", "未保存のMap", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;

    private void SelectProjectFolder()
    {
        var dialog = new OpenFolderDialog { Title = "Unity Projectフォルダーを選択", InitialDirectory = projectRoot ?? Environment.CurrentDirectory };
        if (dialog.ShowDialog(this) == true) OpenProject(dialog.FolderName, null, fit: true);
    }

    private void OpenProject(string root, string? preferredMap, bool fit)
    {
        if (!ProjectPaths.IsUnityProject(root)) throw new InvalidDataException("Assets/Content/Maps/Authoring がありません。Unity Projectフォルダーを選んでください。");
        var loadedCatalog = MapFormat.LoadCatalog(ProjectPaths.CatalogPath(root));
        var maps = ProjectPaths.MapPaths(root);
        if (maps.Length == 0) throw new FileNotFoundException("*.hwmap.json がありません。");
        projectRoot = root;
        catalog = loadedCatalog;
        foreach (var image in spriteCache.Values) image?.Dispose();
        spriteCache.Clear();
        loading = true;
        var choices = maps.Select(path => {
            var document = MapFormat.LoadMap(path);
            return new MapChoice(path, document.MapId, document.DisplayName);
        }).ToArray();
        if (choices.Select(item => item.MapId).Distinct(StringComparer.Ordinal).Count() != choices.Length)
            throw new InvalidDataException("Map IDが重複しています。");
        MapPicker.DisplayMemberPath = nameof(MapChoice.DisplayName);
        MapPicker.ItemsSource = choices;
        var choice = choices.FirstOrDefault(item => string.Equals(item.Path, preferredMap, StringComparison.OrdinalIgnoreCase)) ?? choices[0];
        MapPicker.SelectedItem = choice;
        loading = false;
        LoadMap(choice.Path, fit);
    }

    private void PopulatePalette(MapDocument map)
    {
        if (catalog == null) return;
        loading = true;
        var paletteEntries = catalog.Surfaces.Where(item => item.EditorSelectable).Select(item =>
            new PaletteEntry(item.DefinitionId, item.DisplayName, true, false,
                LoadPaletteIcon(item.GrowsGrass ? catalog.Visuals.GrassSpritePath : item.PreviewSpritePath),
                item.GrowsGrass ? "通常の草地にします" : item.DisplayName + "を配置します")).Concat(
            catalog.Objects.Where(item => item.EditorSelectable).Select(item =>
                new PaletteEntry(item.DefinitionId, item.DisplayName, false, false,
                    LoadPaletteIcon(item.PreviewSpritePath), item.DisplayName + "を配置します",
                    item.Category switch { "nature" => "自然", "furniture" => "家具",
                        "fixture" => "設備", _ => "オブジェクト" }))).Concat(
            catalog.EntityCatalog.Entities.Select(item => new PaletteEntry(item.DefinitionId, item.DisplayName,
                false, false, LoadPaletteIcon(item.PreviewSpritePath), item.DisplayName + "のSpawnを配置します",
                item.EditorCategory, true))).ToArray();
        var paletteView = CollectionViewSource.GetDefaultView(paletteEntries);
        paletteView.Filter = item => item is PaletteEntry entry &&
            (string.IsNullOrWhiteSpace(PaletteSearch.Text) ||
             entry.DisplayName.Contains(PaletteSearch.Text, StringComparison.OrdinalIgnoreCase) ||
             entry.DefinitionId.Contains(PaletteSearch.Text, StringComparison.OrdinalIgnoreCase));
        paletteView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(PaletteEntry.GroupName)));
        PaletteList.ItemsSource = paletteView;
        PaletteList.SelectedIndex = -1;
        loading = false;
    }

    private void LoadMap(string path, bool fit)
    {
        if (catalog == null) return;
        var loaded = MapSession.Open(path, catalog);
        var issues = MapRules.Validate(loaded.Map, catalog);
        session = loaded;
        PopulatePalette(loaded.Map);
        selectedInstanceId = null; selectedCell = null;
        knownMapWriteTime = File.GetLastWriteTimeUtc(path);
        externalChangePending = false;
        StatusText.Text = issues.Count > 0
            ? $"{issues.Count} 件の検証問題があります。保存前に修正してください。"
            : "Mapを読み込みました。Cellを選ぶかPaletteから配置してください。";
        if (fit) FitMap();
        RefreshView();
    }

    private void RefreshView()
    {
        if (session == null || catalog == null) return;
        var map = session.Map;
        Title = "HALKA WORLD MAP EDITOR v0.5 — " + map.MapId + (session.IsDirty ? " *" : "");
        DirtyText.Text = session.IsDirty ? "● 未保存" : "保存済み";
        ActivePaletteText.Text = tool switch
        {
            EditorTool.Select => "選択",
            EditorTool.Erase => "消去",
            _ when PaletteList.SelectedItem is PaletteEntry palette =>
                $"配置: {palette.DisplayName}  |  {(palette.IsSurface ? "地面" : palette.IsEntity ? "Entity" : "オブジェクト")}",
            _ => "配置: 未選択"
        };
        MapInfo.Text = $"{map.DisplayName} ({map.MapId})\nType: {map.MapType}  Base: {map.BaseSurfaceDefinitionId}  Grass: {map.GrassMode}\nBounds: X {map.Bounds.MinX}..{map.Bounds.MaxX}, Y {map.Bounds.MinY}..{map.Bounds.MaxY}\nSurface: {map.Surfaces.Count}\nObject: {map.Objects.Count}\nEntity: {map.EntitySpawns.Count}\nDerived Grass: {CountGrass()}\nTool: {tool}";
        var selected = SelectedObject();
        var selectedEntity = SelectedEntity();
        SelectedKind.Text = selectedEntity != null ?
            $"{catalog.EntityCatalog.ById[selectedEntity.DefinitionId].DisplayName} ({selectedEntity.DefinitionId})\nMap: {map.MapId}\nSpawn: {selectedEntity.Cell}" :
            selected == null ? selectedCell.HasValue ? $"Cell {selectedCell.Value}" : "セルを選択してください" :
            catalog.ObjectById.TryGetValue(selected.DefinitionId, out var definition)
                ? $"{definition.DisplayName} ({selected.DefinitionId})\nMap: {map.MapId}\nRoot: {selected.RootCell}\nVisual: {definition.VisualWidthPixels / 32}×{definition.VisualHeightPixels / 32} cells\nBlocked: {MapRules.FootprintCells(selected, definition).Count()} cells"
                : selected.DefinitionId;
        ActionPointInfo.Text = selected != null && catalog.ObjectById.TryGetValue(selected.DefinitionId, out var actionDefinition)
            ? actionDefinition.ActionPoints.Count == 0 ? "なし" : string.Join("\n\n", actionDefinition.ActionPoints.Select(point =>
                $"{point.Id}\nLocal: {point.PlayerCellOffset}  World: {MapRules.ActionCell(selected, point)}\nFacing: {point.PlayerFacing}  Type: {point.ActionType}\nPose: {point.PoseKey ?? "—"}  Text: {MapRules.ActionText(selected, point) ?? "—"}"))
            : selectedEntity != null ? "Entity Spawn" : "Objectを選択してください";
        FacingSection.Visibility = selectedEntity != null ? Visibility.Visible : Visibility.Collapsed;
        if (selectedEntity != null) FacingPicker.SelectedValue = selectedEntity.Facing;
        SignTextSection.Visibility = selected?.DefinitionId == "sign_basic" ? Visibility.Visible : Visibility.Collapsed;
        if (selected?.DefinitionId == "sign_basic" && !SignTextBox.IsKeyboardFocusWithin)
            SignTextBox.Text = selected.SignText ?? "";
        InstanceText.Text = selected?.InstanceId ?? selectedEntity?.InstanceId ?? "";
        RootX.Text = selected?.RootCell.X.ToString() ?? selectedEntity?.Cell.X.ToString() ?? selectedCell?.X.ToString() ?? "";
        RootY.Text = selected?.RootCell.Y.ToString() ?? selectedEntity?.Cell.Y.ToString() ?? selectedCell?.Y.ToString() ?? "";
        ValidateNow();
        MapCanvas.InvalidateVisual();
    }

    private int CountGrass()
    {
        if (session == null || catalog == null) return 0;
        var b = session.Map.Bounds; var count = 0;
        for (var y = b.MinY; y <= b.MaxY; y++) for (var x = b.MinX; x <= b.MaxX; x++)
            if (MapRules.HasGrass(session.Map, catalog, new GridCell(x, y))) count++;
        return count;
    }

    private ObjectPlacement? SelectedObject() => session?.Map.Objects.FirstOrDefault(item => item.InstanceId == selectedInstanceId);
    private EntitySpawn? SelectedEntity() => session?.Map.EntitySpawns.FirstOrDefault(item => item.InstanceId == selectedInstanceId);

    private void ValidateNow()
    {
        if (session == null || catalog == null) return;
        var issues = MapRules.Validate(session.Map, catalog);
        ValidationList.ItemsSource = issues.Count == 0 ? ["問題なし"] : issues.Select(item => $"{(item.IsWarning ? "WARNING" : "ERROR")} {item.Code}  {item.Cell?.ToString() ?? ""}  {item.Message}").ToArray();
    }

    private void ShowError(string action, Exception error)
    {
        StatusText.Text = action + "失敗: " + error.Message;
        MessageBox.Show(this, error.Message, action + "失敗", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void ChooseProject(object sender, RoutedEventArgs e) { if (ConfirmDiscard()) try { SelectProjectFolder(); } catch (Exception error) { ShowError("Projectを開く", error); } }
    private void ReloadMap(object sender, RoutedEventArgs e) { if (session != null && ConfirmDiscard()) try { LoadMap(session.FilePath, false); } catch (Exception error) { ShowError("再読み込み", error); } }
    private void SaveMap(object sender, RoutedEventArgs e)
    {
        if (session == null) return;
        try
        {
            if (externalChangePending || File.GetLastWriteTimeUtc(session.FilePath) > knownMapWriteTime)
            {
                if (MessageBox.Show(this, "Mapが外部で変更されています。ディスクの新しい内容を上書きしますか？ 前版は.bakへ保存されます。",
                    "外部変更の競合", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
            }
            session.Save(); knownMapWriteTime = File.GetLastWriteTimeUtc(session.FilePath);
            externalChangePending = false;
            StatusText.Text = "保存しました。UnityでImportするとRuntimeへ反映されます。"; RefreshView();
        }
        catch (Exception error) { ShowError("保存", error); }
    }
    private void ExitApp(object sender, RoutedEventArgs e) => Close();
    private void UndoClick(object sender, RoutedEventArgs e) { session?.Undo(); RefreshView(); }
    private void RedoClick(object sender, RoutedEventArgs e) { session?.Redo(); RefreshView(); }
    private void DeleteClick(object sender, RoutedEventArgs e)
    {
        if (session == null || selectedInstanceId == null) return;
        var entity = SelectedEntity();
        if (entity?.DefinitionId == "player_main" && MessageBox.Show(this,
            "ﾊﾙｶﾁｬﾝ開始位置を削除しますか？ 保存前に再配置が必要です。", "開始位置の削除",
            MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        if (entity != null ? session.DeleteEntity(selectedInstanceId) : session.DeleteObject(selectedInstanceId))
        { selectedInstanceId = null; RefreshView(); }
    }
    private void ValidateClick(object sender, RoutedEventArgs e) => ValidateNow();
    private void AboutClick(object sender, RoutedEventArgs e) => MessageBox.Show(this, "HALKA WORLD MAP EDITOR v0.5\nStandalone Edition\nMap JSONを編集します。UnityのMapDefinitionは生成キャッシュです。", "このツールについて");
    private void PaletteSearchChanged(object sender, TextChangedEventArgs e)
    {
        if (PaletteList?.ItemsSource is { } source)
            CollectionViewSource.GetDefaultView(source)?.Refresh();
    }
    private void SelectToolClick(object sender, RoutedEventArgs e) { tool = EditorTool.Select; PaletteList.SelectedIndex = -1; RefreshView(); }
    private void PaintToolClick(object sender, RoutedEventArgs e) { tool = EditorTool.Paint; if (PaletteList.SelectedIndex < 0) PaletteList.SelectedIndex = 0; RefreshView(); }
    private void EraseToolClick(object sender, RoutedEventArgs e) { tool = EditorTool.Erase; PaletteList.SelectedIndex = -1; RefreshView(); }
    private void FitClick(object sender, RoutedEventArgs e) { FitMap(); MapCanvas.InvalidateVisual(); }
    private void PaletteChanged(object sender, SelectionChangedEventArgs e)
    {
        if (loading || PaletteList.SelectedItem is not PaletteEntry entry) return;
        tool = EditorTool.Paint;
        StatusText.Text = $"配置: {entry.DisplayName}";
        RefreshView();
    }

    private BitmapImage? LoadPaletteIcon(string relative)
    {
        if (projectRoot == null) return null;
        try
        {
            var path = ProjectPaths.SpritePath(projectRoot, relative);
            if (!File.Exists(path)) return null;
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch { return null; }
    }
    private void OverlayChanged(object sender, RoutedEventArgs e) => MapCanvas?.InvalidateVisual();
    private void MapPickerChanged(object sender, SelectionChangedEventArgs e)
    {
        if (loading || projectRoot == null || MapPicker.SelectedItem is not MapChoice choice) return;
        var path = choice.Path;
        if (session?.FilePath == path) return;
        if (!ConfirmMapSwitch())
        {
            loading = true;
            MapPicker.SelectedItem = MapPicker.Items.OfType<MapChoice>().FirstOrDefault(item => item.Path == session?.FilePath);
            loading = false;
            return;
        }
        try { LoadMap(path, true); } catch (Exception error) { ShowError("Map切替", error); }
    }

    private bool ConfirmMapSwitch()
    {
        if (session?.IsDirty != true) return true;
        var answer = MessageBox.Show(this, "未保存の変更があります。保存してMapを切り替えますか？\nはい: 保存 / いいえ: 破棄 / キャンセル: 切替を中止",
            "未保存のMap", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
        if (answer == MessageBoxResult.Cancel) return false;
        if (answer == MessageBoxResult.Yes) { SaveMap(this, new RoutedEventArgs()); return !session.IsDirty; }
        return true;
    }
    private void MoveObjectClick(object sender, RoutedEventArgs e)
    {
        if (session == null || selectedInstanceId == null) return;
        if (!int.TryParse(RootX.Text, out var x) || !int.TryParse(RootY.Text, out var y)) { StatusText.Text = "座標は整数で入力してください。"; return; }
        string reason;
        var moved = SelectedEntity() != null
            ? session.MoveEntity(selectedInstanceId, new GridCell(x, y), out reason)
            : session.MoveObject(selectedInstanceId, new GridCell(x, y), out reason);
        if (!moved) StatusText.Text = reason;
        RefreshView();
    }
    private void ApplyFacingClick(object sender, RoutedEventArgs e)
    {
        if (session != null && selectedInstanceId != null && FacingPicker.SelectedValue is string facing)
            session.SetEntityFacing(selectedInstanceId, facing);
        RefreshView();
    }
    private void ApplySignTextClick(object sender, RoutedEventArgs e)
    {
        if (session == null || selectedInstanceId == null) return;
        if (string.IsNullOrWhiteSpace(SignTextBox.Text))
        { StatusText.Text = "看板の内容を入力してください。"; return; }
        session.SetSignText(selectedInstanceId, SignTextBox.Text);
        SignTextBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        RefreshView();
    }
    private void SignTextKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Return) return;
        ApplySignTextClick(sender, e);
        e.Handled = true;
    }
    private void ValidationDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (session == null || catalog == null || ValidationList.SelectedIndex < 0) return;
        var issues = MapRules.Validate(session.Map, catalog);
        if (ValidationList.SelectedIndex >= issues.Count || issues[ValidationList.SelectedIndex].Cell is not { } cell) return;
        selectedCell = cell; panX = -(cell.X + .5) * zoom; panY = (cell.Y + .5) * zoom; RefreshView();
    }
    private void WindowKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            if (e.Key == Key.S) SaveMap(sender, e);
            else if (e.Key == Key.Z) UndoClick(sender, e);
            else if (e.Key == Key.Y) RedoClick(sender, e);
            else if (e.Key == Key.C && SelectedObject() is { } source)
                Clipboard.SetText("HALKA_WORLD_OBJECT:" + JsonSerializer.Serialize(source));
            else if (e.Key == Key.C && SelectedEntity() is { } entity)
            {
                if (entity.DefinitionId == "player_main") StatusText.Text = "ﾊﾙｶﾁｬﾝ開始位置はコピーできません。";
                else Clipboard.SetText("HALKA_WORLD_ENTITY:" + JsonSerializer.Serialize(entity));
            }
            else if (e.Key == Key.V && selectedCell is { } cell && Clipboard.ContainsText() && session != null)
            {
                var clip = Clipboard.GetText();
                ObjectPlacement? copied = null;
                EntitySpawn? copiedEntity = null;
                if (clip.StartsWith("HALKA_WORLD_OBJECT:", StringComparison.Ordinal))
                    try { copied = JsonSerializer.Deserialize<ObjectPlacement>(clip[19..]); }
                    catch (JsonException) { StatusText.Text = "コピーしたObjectを読み込めません。"; }
                if (clip.StartsWith("HALKA_WORLD_ENTITY:", StringComparison.Ordinal))
                    try { copiedEntity = JsonSerializer.Deserialize<EntitySpawn>(clip[19..]); }
                    catch (JsonException) { StatusText.Text = "コピーしたEntityを読み込めません。"; }
                if (copiedEntity != null)
                { if (!session.PlaceEntity(copiedEntity.DefinitionId, cell, out var reason)) StatusText.Text = reason;
                  else session.SetEntityFacing(session.Map.EntitySpawns.Last(item => item.Cell == cell).InstanceId, copiedEntity.Facing); }
                else if (copied != null)
                { if (!session.PlaceObject(copied.DefinitionId, cell, out var reason, copied.SignText)) StatusText.Text = reason; }
                else if (!clip.StartsWith("HALKA_WORLD_OBJECT:", StringComparison.Ordinal) &&
                    !clip.StartsWith("HALKA_WORLD_ENTITY:", StringComparison.Ordinal) &&
                    !session.PlaceObject(clip, cell, out var reason)) StatusText.Text = reason;
                RefreshView();
            }
            else return;
            e.Handled = true; return;
        }
        if (Keyboard.FocusedElement is TextBox) return;
        if (e.Key == Key.D1) { tool = EditorTool.Select; PaletteList.SelectedIndex = -1; }
        else if (e.Key == Key.D2) { tool = EditorTool.Paint; if (PaletteList.SelectedIndex < 0) PaletteList.SelectedIndex = 0; }
        else if (e.Key == Key.D3) { tool = EditorTool.Erase; PaletteList.SelectedIndex = -1; }
        else if (e.Key == Key.F) FitMap();
        else if (e.Key == Key.G) GridCheck.IsChecked = GridCheck.IsChecked != true;
        else if (e.Key == Key.Delete) DeleteClick(sender, e);
        else return;
        e.Handled = true; RefreshView();
    }

    private GridCell CellAt(Point point) => new((int)Math.Floor((point.X - MapCanvas.ActualWidth / 2 - panX) / zoom),
        (int)Math.Floor((MapCanvas.ActualHeight / 2 + panY - point.Y) / zoom));
    private Rect CellRect(GridCell cell) => new(MapCanvas.ActualWidth / 2 + panX + cell.X * zoom,
        MapCanvas.ActualHeight / 2 + panY - (cell.Y + 1) * zoom, zoom, zoom);
    private void FitMap()
    {
        if (session == null || MapCanvas.ActualWidth < 1 || MapCanvas.ActualHeight < 1) return;
        var b = session.Map.Bounds;
        zoom = Math.Clamp(Math.Min((MapCanvas.ActualWidth - 50) / (b.MaxX - b.MinX + 1),
            (MapCanvas.ActualHeight - 50) / (b.MaxY - b.MinY + 1)), 8, 160);
        panX = -(b.MinX + b.MaxX + 1) * zoom / 2;
        panY = (b.MinY + b.MaxY + 1) * zoom / 2;
    }

    private void CanvasMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (session == null || catalog == null) return;
        var point = e.GetPosition(MapCanvas);
        lastPointer = point;
        if (e.ChangedButton == MouseButton.Middle || (e.ChangedButton == MouseButton.Left && Keyboard.IsKeyDown(Key.Space)))
        { dragPan = true; MapCanvas.CaptureMouse(); return; }
        var cell = CellAt(point);
        if (!session.Map.Bounds.Contains(cell)) return;
        selectedCell = cell;
        rightErase = e.ChangedButton == MouseButton.Right;
        if (tool == EditorTool.Select && !rightErase)
        {
            selectedInstanceId = HitEntity(point)?.InstanceId ?? HitObject(point)?.InstanceId;
        }
        else if (tool == EditorTool.Erase || rightErase)
        {
            session.BeginStroke(); dragPaint = true; EraseCell(point, cell);
        }
        else if (PaletteList.SelectedItem is PaletteEntry entry)
        {
            session.BeginStroke(); dragPaint = entry.IsSurface;
            if (dragPaint) lastPaintCell = cell;
            if (entry.IsSurface)
            {
                if (!session.PaintSurface(entry.DefinitionId, cell) &&
                    session.Map.Objects.Any(item => catalog.ObjectById.TryGetValue(item.DefinitionId, out var definition) &&
                        MapRules.FootprintCells(item, definition).Contains(cell)))
                    StatusText.Text = "Objectの占有セルには地面を配置できません。";
            }
            else if (entry.PreviewIcon == null) StatusText.Text = "MISSING ASSET: 正式Spriteがないため配置できません。";
            else if (entry.IsEntity)
            { if (!session.PlaceEntity(entry.DefinitionId, cell, out var entityReason)) StatusText.Text = entityReason; }
            else if (!session.PlaceObject(entry.DefinitionId, cell, out var reason)) StatusText.Text = reason;
        }
        MapCanvas.CaptureMouse(); RefreshView();
    }

    private ObjectPlacement? HitObject(Point point)
    {
        if (session == null || catalog == null) return null;
        foreach (var item in session.Map.Objects.OrderBy(item =>
            catalog.ObjectById.TryGetValue(item.DefinitionId, out var kind)
                ? kind.VisualWidthPixels * kind.VisualHeightPixels : int.MaxValue))
        {
            if (!catalog.ObjectById.TryGetValue(item.DefinitionId, out var definition)) continue;
            if (VisualRect(item, definition).Contains(point)) return item;
        }
        return null;
    }

    private EntitySpawn? HitEntity(Point point)
    {
        if (session == null || catalog == null || EntityCheck.IsChecked != true) return null;
        return session.Map.EntitySpawns.FirstOrDefault(item =>
            catalog.EntityCatalog.ById.TryGetValue(item.DefinitionId, out var def) &&
            EntityRect(item, def).Contains(point));
    }

    private Rect EntityRect(EntitySpawn item, CatalogEntity definition)
    {
        var cell = CellRect(item.Cell);
        return new Rect(cell.Left - (definition.VisualWidthCells - 1) * zoom / 2,
            cell.Bottom - definition.VisualHeightCells * zoom,
            definition.VisualWidthCells * zoom, definition.VisualHeightCells * zoom);
    }

    private Rect VisualRect(ObjectPlacement item, CatalogObject definition)
    {
        var bounds = MapRules.VisualBounds(item, definition);
        var low = CellRect(bounds.Minimum);
        var high = CellRect(bounds.Maximum);
        return new Rect(low.Left, high.Top, high.Right - low.Left, low.Bottom - high.Top);
    }

    private void EraseCell(Point point, GridCell cell)
    {
        if (session == null) return;
        var selected = HitObject(point);
        var entity = HitEntity(point);
        if (entity != null) { selectedInstanceId = entity.InstanceId; DeleteClick(this, new RoutedEventArgs()); }
        else if (selected != null) { session.DeleteObject(selected.InstanceId); if (selectedInstanceId == selected.InstanceId) selectedInstanceId = null; }
        else session.EraseSurface(cell);
    }

    private void CanvasMouseMove(object sender, MouseEventArgs e)
    {
        if (session == null || catalog == null) return;
        var point = e.GetPosition(MapCanvas);
        if (dragPan)
        { panX += point.X - lastPointer.X; panY += point.Y - lastPointer.Y; lastPointer = point; MapCanvas.InvalidateVisual(); return; }
        var cell = CellAt(point);
        StatusText.Text = session.Map.Bounds.Contains(cell) ? $"Cell {cell}   Surface: {(session.Map.Surfaces.FirstOrDefault(item => item.Cell == cell)?.DefinitionId ?? session.Map.BaseSurfaceDefinitionId)}   Zoom {zoom:0.#} px/cell" : "Map範囲外";
        if (dragPaint && session.Map.Bounds.Contains(cell))
        {
            if (rightErase || tool == EditorTool.Erase) EraseCell(point, cell);
            else if (PaletteList.SelectedItem is PaletteEntry { IsSurface: true } entry)
            {
                foreach (var crossed in StrokeCells(lastPaintCell ?? cell, cell))
                {
                    if (!session.Map.Bounds.Contains(crossed)) continue;
                    session.PaintSurface(entry.DefinitionId, crossed);
                }
                lastPaintCell = cell;
            }
            RefreshView();
        }
    }

    private static IEnumerable<GridCell> StrokeCells(GridCell from, GridCell to)
    {
        var x = from.X;
        var y = from.Y;
        var dx = Math.Abs(to.X - x);
        var dy = -Math.Abs(to.Y - y);
        var sx = x < to.X ? 1 : -1;
        var sy = y < to.Y ? 1 : -1;
        var error = dx + dy;
        while (true)
        {
            yield return new GridCell(x, y);
            if (x == to.X && y == to.Y) yield break;
            var twice = 2 * error;
            if (twice >= dy) { error += dy; x += sx; }
            if (twice <= dx) { error += dx; y += sy; }
        }
    }

    private void CanvasMouseUp(object sender, MouseButtonEventArgs e)
    {
        dragPan = false; dragPaint = false; rightErase = false; lastPaintCell = null;
        session?.EndStroke(); MapCanvas.ReleaseMouseCapture(); RefreshView();
    }
    private void CanvasMouseLeave(object sender, MouseEventArgs e) { if (!MapCanvas.IsMouseCaptured) session?.EndStroke(); }
    private void CanvasMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var point = e.GetPosition(MapCanvas);
        var worldX = (point.X - MapCanvas.ActualWidth / 2 - panX) / zoom;
        var worldY = (MapCanvas.ActualHeight / 2 + panY - point.Y) / zoom;
        zoom = Math.Clamp(zoom * (e.Delta > 0 ? 1.2 : 1 / 1.2), 8, 160);
        panX = point.X - MapCanvas.ActualWidth / 2 - worldX * zoom;
        panY = point.Y - MapCanvas.ActualHeight / 2 + worldY * zoom;
        MapCanvas.InvalidateVisual(); e.Handled = true;
    }

    private SKBitmap? Sprite(string relative)
    {
        if (projectRoot == null || string.IsNullOrWhiteSpace(relative)) return null;
        if (spriteCache.TryGetValue(relative, out var bitmap)) return bitmap;
        try
        {
            var path = ProjectPaths.SpritePath(projectRoot, relative);
            bitmap = File.Exists(path) ? SKBitmap.Decode(path) : null;
        }
        catch { bitmap = null; }
        spriteCache[relative] = bitmap;
        if (bitmap == null) StatusText.Text = $"ERROR: Spriteを読み込めません: {relative}";
        return bitmap;
    }

    private static void Fill(SKCanvas canvas, SKRect rect, SKColor color)
    { using var paint = new SKPaint { Color = color, IsAntialias = false }; canvas.DrawRect(rect, paint); }
    private static void Text(SKCanvas canvas, string value, float x, float y, SKColor color, float size)
    { using var font = new SKFont(MarkerTypeface, size); using var paint = new SKPaint { Color = color, IsAntialias = false }; canvas.DrawText(value, x, y, SKTextAlign.Left, font, paint); }
    private void DrawSprite(SKCanvas canvas, string path, SKRect rect, SKColor fallback)
    {
        var image = Sprite(path);
        if (image == null) { Fill(canvas, rect, fallback); return; }
        using var paint = new SKPaint { IsAntialias = false };
        using var skImage = SKImage.FromBitmap(image);
        canvas.DrawImage(skImage, rect, new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None), paint);
    }

    private SKRect ToSkia(Rect rect) => new((float)rect.Left, (float)rect.Top, (float)rect.Right, (float)rect.Bottom);
    private void MapCanvasPaint(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(session?.Map.MapType == "interior" ? new SKColor(16, 16, 20) : new SKColor(19, 25, 25));
        if (session == null || catalog == null) return;
        var sx = (float)(e.Info.Width / Math.Max(1, MapCanvas.ActualWidth));
        var sy = (float)(e.Info.Height / Math.Max(1, MapCanvas.ActualHeight));
        canvas.Scale(sx, sy);
        var map = session.Map;
        var b = map.Bounds;
        var minX = Math.Max(b.MinX, CellAt(new Point(0, MapCanvas.ActualHeight)).X - 1);
        var maxX = Math.Min(b.MaxX, CellAt(new Point(MapCanvas.ActualWidth, 0)).X + 1);
        var minY = Math.Max(b.MinY, CellAt(new Point(0, MapCanvas.ActualHeight)).Y - 1);
        var maxY = Math.Min(b.MaxY, CellAt(new Point(MapCanvas.ActualWidth, 0)).Y + 1);
        var surfaceLookup = map.Surfaces.ToDictionary(item => item.Cell);
        for (var y = minY; y <= maxY; y++) for (var x = minX; x <= maxX; x++)
        {
            var cell = new GridCell(x, y); var rect = ToSkia(CellRect(cell));
            if (map.MapType == "interior" && catalog.SurfaceById.TryGetValue(map.BaseSurfaceDefinitionId, out var baseSurface))
                DrawSprite(canvas, baseSurface.PreviewSpritePath, rect, new SKColor(86, 68, 52));
            else Fill(canvas, rect, new SKColor(130, 151, 89));
            if (surfaceLookup.TryGetValue(cell, out var surface) && catalog.SurfaceById.TryGetValue(surface.DefinitionId, out var sdef))
            {
                if (sdef.GrowsGrass) Fill(canvas, rect, new SKColor(217, 232, 184));
                else DrawSprite(canvas, sdef.PreviewSpritePath, rect, new SKColor(125, 101, 75));
            }
            if (GrassCheck.IsChecked == true && MapRules.HasGrass(map, catalog, cell))
                DrawSprite(canvas, catalog.Visuals.GrassSpritePath, rect, new SKColor(60, 117, 43));
            if (GridCheck.IsChecked == true)
            { using var line = new SKPaint { Color = new SKColor(15, 28, 14, 110), StrokeWidth = 1, Style = SKPaintStyle.Stroke, IsAntialias = false }; canvas.DrawRect(rect, line); }
            if (CoordCheck.IsChecked == true && zoom >= 25) Text(canvas, $"{x},{y}", rect.Left + 2, rect.Top + Math.Min(12, (float)zoom / 3), SKColors.Black, Math.Clamp((float)zoom / 4, 8, 12));
        }
        foreach (var item in map.Objects)
        {
            if (!catalog.ObjectById.TryGetValue(item.DefinitionId, out var def)) continue;
            var imageRect = VisualRect(item, def);
            DrawSprite(canvas, def.PreviewSpritePath, ToSkia(imageRect), SKColors.DarkSlateGray);
            if (item.InstanceId == selectedInstanceId)
            {
                using var line = new SKPaint { Color = SKColors.Yellow, Style = SKPaintStyle.Stroke, StrokeWidth = 2 };
                canvas.DrawRect(ToSkia(imageRect), line);
                foreach (var blocked in MapRules.FootprintCells(item, def))
                    Fill(canvas, ToSkia(CellRect(blocked)), new SKColor(255, 70, 40, 85));
                var root = ToSkia(CellRect(item.RootCell));
                Text(canvas, "R", root.MidX - 5, root.MidY + 5, SKColors.Yellow, 16);
            }
        }
        if (CollisionCheck.IsChecked == true)
            for (var y = minY; y <= maxY; y++) for (var x = minX; x <= maxX; x++)
            {
                var cell = new GridCell(x, y);
                if (MapRules.BlocksMovement(map, catalog, cell))
                    Fill(canvas, ToSkia(CellRect(cell)), new SKColor(255, 55, 55, 75));
            }
        if (EntityCheck.IsChecked == true)
        {
            foreach (var spawn in map.EntitySpawns)
            {
                if (!catalog.EntityCatalog.ById.TryGetValue(spawn.DefinitionId, out var def)) continue;
                if (def.WanderRegion is { } wander)
                {
                    var a = CellRect(new GridCell(spawn.Cell.X + wander.MinX, spawn.Cell.Y + wander.MinY));
                    var far = CellRect(new GridCell(spawn.Cell.X + wander.MaxX, spawn.Cell.Y + wander.MaxY));
                    using var regionPaint = new SKPaint {
                        Color = spawn.InstanceId == selectedInstanceId ? new SKColor(255, 225, 90, 180) : new SKColor(255, 225, 90, 60),
                        Style = SKPaintStyle.Stroke, StrokeWidth = spawn.InstanceId == selectedInstanceId ? 2 : 1 };
                    canvas.DrawRect(new SKRect((float)a.Left, (float)far.Top, (float)far.Right, (float)a.Bottom), regionPaint);
                }
                var rect = EntityRect(spawn, def);
                DrawSprite(canvas, def.PreviewSpritePath, ToSkia(rect), SKColors.DimGray);
                if (spawn.InstanceId == selectedInstanceId)
                {
                    using var outline = new SKPaint { Color = SKColors.Yellow, Style = SKPaintStyle.Stroke, StrokeWidth = 2 };
                    canvas.DrawRect(ToSkia(rect), outline);
                    var center = ToSkia(CellRect(spawn.Cell));
                    Text(canvas, spawn.Facing switch { "up" => "↑", "down" => "↓", "left" => "←", _ => "→" },
                        center.MidX - 7, center.Bottom - 4, SKColors.Yellow, 16);
                }
            }
        }
        if (MarkerCheck.IsChecked == true)
        {
            foreach (var marker in map.Markers)
                Marker(canvas, marker.Cell, marker.Id, marker.Id.StartsWith("road_") ? SKColors.Cyan : SKColors.White);
            if (MapRules.HouseRoot(map) is { } door) Marker(canvas, door, "家入口", SKColors.Gold);
            if (MapRules.OutsideEntry(map) is { } entry) Marker(canvas, entry, "家前", SKColors.Gold);
        }
        if (ActionPointCheck.IsChecked == true)
        {
            foreach (var item in map.Objects)
            {
                if (!catalog.ObjectById.TryGetValue(item.DefinitionId, out var definition)) continue;
                var highlighted = item.InstanceId == selectedInstanceId;
                foreach (var action in definition.ActionPoints)
                {
                    var cell = MapRules.ActionCell(item, action);
                    if (!map.Bounds.Contains(cell)) continue;
                    var rect = ToSkia(CellRect(cell));
                    var color = highlighted ? SKColors.Gold : new SKColor(255, 235, 140, 180);
                    if (highlighted) Fill(canvas, new SKRect(rect.MidX - 12, rect.MidY - 10, rect.MidX + 12, rect.MidY + 10), new SKColor(22, 28, 40, 210));
                    var symbol = action.ActionType switch { "examine" => "E", "sit" => "S", "sleep" => "Z", _ => "•" };
                    var arrow = action.PlayerFacing switch { "up" => "↑", "down" => "↓", "left" => "←", "right" => "→", _ => "?" };
                    Text(canvas, symbol + arrow, rect.MidX - (highlighted ? 10 : 7), rect.MidY + 4,
                        color, highlighted ? 14 : 10);
                }
            }
        }
        if (selectedCell is { } active)
        { using var outline = new SKPaint { Color = SKColors.Yellow, Style = SKPaintStyle.Stroke, StrokeWidth = 2 }; canvas.DrawRect(ToSkia(CellRect(active)), outline); }
    }

    private void DrawLargeMarker(SKCanvas canvas, GridCell root, int width, int height, string path)
    {
        var cell = CellRect(root);
        var rect = new Rect(cell.Left - (width - 1) * zoom / 2, cell.Bottom - height * zoom, width * zoom, height * zoom);
        DrawSprite(canvas, path, ToSkia(rect), SKColors.DimGray);
    }
    private void Marker(SKCanvas canvas, GridCell cell, string label, SKColor color)
    {
        var rect = ToSkia(CellRect(cell));
        Text(canvas, label, rect.Left + 2, rect.Bottom - 3, color, Math.Clamp((float)zoom / 3, 9, 14));
    }
}
