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
        bool IsGrass, BitmapImage? PreviewIcon, string Tooltip)
    {
        public string GroupName => IsSurface ? "地面" : "オブジェクト";
    }
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
        MapPicker.ItemsSource = maps.Select(Path.GetFileName).ToArray();
        var paletteEntries = new[] {
            new PaletteEntry("", "草", true, true, LoadPaletteIcon(catalog.Visuals.GrassSpritePath),
                "通常の草地に戻します")
        }.Concat(catalog.Surfaces.Where(item => item.EditorSelectable).Select(item =>
            new PaletteEntry(item.DefinitionId, item.DisplayName, true, false,
                LoadPaletteIcon(item.PreviewSpritePath), item.DisplayName + "を配置します"))).Concat(
            catalog.Objects.Where(item => item.EditorSelectable).Select(item =>
                new PaletteEntry(item.DefinitionId, item.DisplayName, false, false,
                    LoadPaletteIcon(item.PreviewSpritePath), item.DisplayName + "を配置します"))).ToArray();
        var paletteView = CollectionViewSource.GetDefaultView(paletteEntries);
        paletteView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(PaletteEntry.GroupName)));
        PaletteList.ItemsSource = paletteView;
        PaletteList.SelectedIndex = -1;
        var choice = maps.FirstOrDefault(item => string.Equals(item, preferredMap, StringComparison.OrdinalIgnoreCase)) ?? maps[0];
        MapPicker.SelectedItem = Path.GetFileName(choice);
        loading = false;
        LoadMap(choice, fit);
    }

    private void LoadMap(string path, bool fit)
    {
        if (catalog == null) return;
        var loaded = MapSession.Open(path, catalog);
        var issues = MapRules.Validate(loaded.Map, catalog);
        session = loaded;
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
        Title = "HALKA WORLD MAP EDITOR v0.2 — " + map.MapId + (session.IsDirty ? " *" : "");
        DirtyText.Text = session.IsDirty ? "● 未保存" : "保存済み";
        ActivePaletteText.Text = tool switch
        {
            EditorTool.Select => "選択",
            EditorTool.Erase => "消去",
            _ when PaletteList.SelectedItem is PaletteEntry palette =>
                $"配置: {palette.DisplayName}  |  {(palette.IsSurface ? "地面" : "オブジェクト")}",
            _ => "配置: 未選択"
        };
        MapInfo.Text = $"{map.DisplayName}\nBounds: X {map.Bounds.MinX}..{map.Bounds.MaxX}, Y {map.Bounds.MinY}..{map.Bounds.MaxY}\nSurface: {map.Surfaces.Count}\nObject: {map.Objects.Count}\nGrass: {CountGrass()}\nTool: {tool}";
        var selected = SelectedObject();
        SelectedKind.Text = selected == null ? selectedCell.HasValue ? $"Cell {selectedCell.Value}" : "セルを選択してください" :
            $"{catalog.ObjectById.GetValueOrDefault(selected.DefinitionId)?.DisplayName ?? selected.DefinitionId}  {selected.RootCell}";
        InstanceText.Text = selected?.InstanceId ?? "";
        RootX.Text = selected?.RootCell.X.ToString() ?? selectedCell?.X.ToString() ?? "";
        RootY.Text = selected?.RootCell.Y.ToString() ?? selectedCell?.Y.ToString() ?? "";
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

    private void ValidateNow()
    {
        if (session == null || catalog == null) return;
        var issues = MapRules.Validate(session.Map, catalog);
        ValidationList.ItemsSource = issues.Count == 0 ? ["問題なし"] : issues.Select(item => $"{item.Code}  {item.Cell?.ToString() ?? ""}  {item.Message}").ToArray();
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
    private void DeleteClick(object sender, RoutedEventArgs e) { if (session != null && selectedInstanceId != null && session.DeleteObject(selectedInstanceId)) { selectedInstanceId = null; RefreshView(); } }
    private void ValidateClick(object sender, RoutedEventArgs e) => ValidateNow();
    private void AboutClick(object sender, RoutedEventArgs e) => MessageBox.Show(this, "HALKA WORLD MAP EDITOR v0.2\nStandalone Edition\nMap JSONを編集します。UnityのMapDefinitionは生成キャッシュです。", "このツールについて");
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
        if (loading || projectRoot == null || MapPicker.SelectedItem is not string name) return;
        var path = Path.Combine(ProjectPaths.AuthoringFolder(projectRoot), name);
        if (session?.FilePath == path) return;
        if (!ConfirmDiscard()) { loading = true; MapPicker.SelectedItem = Path.GetFileName(session?.FilePath); loading = false; return; }
        try { LoadMap(path, true); } catch (Exception error) { ShowError("Map切替", error); }
    }
    private void MoveObjectClick(object sender, RoutedEventArgs e)
    {
        if (session == null || selectedInstanceId == null) return;
        if (!int.TryParse(RootX.Text, out var x) || !int.TryParse(RootY.Text, out var y)) { StatusText.Text = "座標は整数で入力してください。"; return; }
        if (!session.MoveObject(selectedInstanceId, new GridCell(x, y), out var reason)) StatusText.Text = reason;
        RefreshView();
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
            else if (e.Key == Key.C && SelectedObject() is { } source) Clipboard.SetText(source.DefinitionId);
            else if (e.Key == Key.V && selectedCell is { } cell && Clipboard.ContainsText() && session != null)
            { if (!session.PlaceObject(Clipboard.GetText(), cell, out var reason)) StatusText.Text = reason; RefreshView(); }
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
            selectedInstanceId = HitObject(point)?.InstanceId;
        }
        else if (tool == EditorTool.Erase || rightErase)
        {
            session.BeginStroke(); dragPaint = true; EraseCell(point, cell);
        }
        else if (PaletteList.SelectedItem is PaletteEntry entry)
        {
            session.BeginStroke(); dragPaint = entry.IsSurface;
            if (dragPaint) lastPaintCell = cell;
            if (entry.IsGrass) session.RestoreGrass(cell);
            else if (entry.IsSurface)
            {
                if (!session.PaintSurface(entry.DefinitionId, cell) &&
                    session.Map.Objects.Any(item => catalog.ObjectById.TryGetValue(item.DefinitionId, out var definition) &&
                        MapRules.FootprintCells(item, definition).Contains(cell)))
                    StatusText.Text = "Objectの占有セルには地面を配置できません。";
            }
            else if (!session.PlaceObject(entry.DefinitionId, cell, out var reason)) StatusText.Text = reason;
        }
        MapCanvas.CaptureMouse(); RefreshView();
    }

    private ObjectPlacement? HitObject(Point point)
    {
        if (session == null || catalog == null) return null;
        foreach (var item in session.Map.Objects.AsEnumerable().Reverse())
        {
            if (!catalog.ObjectById.TryGetValue(item.DefinitionId, out var definition)) continue;
            var anchor = CellRect(item.RootCell);
            var width = zoom * definition.VisualWidthPixels / 32.0;
            var height = zoom * definition.VisualHeightPixels / 32.0;
            if (new Rect(anchor.Left + (zoom - width) / 2, anchor.Bottom - height, width, height).Contains(point)) return item;
        }
        return null;
    }

    private void EraseCell(Point point, GridCell cell)
    {
        if (session == null) return;
        var selected = HitObject(point);
        if (selected != null) { session.DeleteObject(selected.InstanceId); if (selectedInstanceId == selected.InstanceId) selectedInstanceId = null; }
        else session.EraseSurface(cell);
    }

    private void CanvasMouseMove(object sender, MouseEventArgs e)
    {
        if (session == null || catalog == null) return;
        var point = e.GetPosition(MapCanvas);
        if (dragPan)
        { panX += point.X - lastPointer.X; panY += point.Y - lastPointer.Y; lastPointer = point; MapCanvas.InvalidateVisual(); return; }
        var cell = CellAt(point);
        StatusText.Text = session.Map.Bounds.Contains(cell) ? $"Cell {cell}   Surface: {(session.Map.Surfaces.FirstOrDefault(item => item.Cell == cell)?.DefinitionId ?? "grass")}   Zoom {zoom:0.#} px/cell" : "Map範囲外";
        if (dragPaint && session.Map.Bounds.Contains(cell))
        {
            if (rightErase || tool == EditorTool.Erase) EraseCell(point, cell);
            else if (PaletteList.SelectedItem is PaletteEntry { IsSurface: true } entry)
            {
                foreach (var crossed in StrokeCells(lastPaintCell ?? cell, cell))
                {
                    if (!session.Map.Bounds.Contains(crossed)) continue;
                    if (entry.IsGrass) session.RestoreGrass(crossed);
                    else session.PaintSurface(entry.DefinitionId, crossed);
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
        canvas.Clear(new SKColor(19, 25, 25));
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
            Fill(canvas, rect, new SKColor(130, 151, 89));
            if (surfaceLookup.TryGetValue(cell, out var surface) && catalog.SurfaceById.TryGetValue(surface.DefinitionId, out var sdef))
                DrawSprite(canvas, sdef.PreviewSpritePath, rect, new SKColor(125, 101, 75));
            else if (GrassCheck.IsChecked == true && MapRules.HasGrass(map, catalog, cell))
                DrawSprite(canvas, catalog.Visuals.GrassSpritePath, rect, new SKColor(60, 117, 43));
            if (CollisionCheck.IsChecked == true && MapRules.BlocksMovement(map, catalog, cell))
                Fill(canvas, rect, new SKColor(255, 55, 55, 100));
            if (GridCheck.IsChecked == true)
            { using var line = new SKPaint { Color = new SKColor(15, 28, 14, 110), StrokeWidth = 1, Style = SKPaintStyle.Stroke, IsAntialias = false }; canvas.DrawRect(rect, line); }
            if (CoordCheck.IsChecked == true && zoom >= 25) Text(canvas, $"{x},{y}", rect.Left + 2, rect.Top + Math.Min(12, (float)zoom / 3), SKColors.Black, Math.Clamp((float)zoom / 4, 8, 12));
        }
        foreach (var item in map.Objects)
        {
            if (!catalog.ObjectById.TryGetValue(item.DefinitionId, out var def)) continue;
            var root = CellRect(item.RootCell);
            var width = zoom * def.VisualWidthPixels / 32.0;
            var height = zoom * def.VisualHeightPixels / 32.0;
            var imageRect = new Rect(root.Left + (zoom - width) / 2, root.Bottom - height, width, height);
            DrawSprite(canvas, def.PreviewSpritePath, ToSkia(imageRect), SKColors.DarkSlateGray);
            if (item.InstanceId == selectedInstanceId)
            { using var line = new SKPaint { Color = SKColors.Yellow, Style = SKPaintStyle.Stroke, StrokeWidth = 2 }; canvas.DrawRect(ToSkia(imageRect), line); }
        }
        DrawLargeMarker(canvas, map.Markers.HouseDoor, 5, 4, catalog.Visuals.HouseSpritePath);
        DrawLargeMarker(canvas, map.Markers.PlayerSpawn, 2, 2, catalog.Visuals.PlayerSpritePath);
        DrawLargeMarker(canvas, map.Markers.CrowSpawn, 1, 1, catalog.Visuals.CrowSpritePath);
        if (MarkerCheck.IsChecked == true)
        {
            Marker(canvas, map.Markers.HouseDoor, "家入口", SKColors.Gold);
            Marker(canvas, map.Markers.OutsideEntry, "家前", SKColors.Gold);
            Marker(canvas, map.Markers.PlayerSpawn, "P", SKColors.White);
            Marker(canvas, map.Markers.CrowSpawn, "C", SKColors.White);
            foreach (var cell in MapRules.RoadEndCells(map)) Marker(canvas, cell, "出口予定", SKColors.Cyan);
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
