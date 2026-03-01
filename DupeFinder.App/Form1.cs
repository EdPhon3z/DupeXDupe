
using System.ComponentModel;
using System.Globalization;
using Microsoft.VisualBasic.FileIO;

namespace DupeXDupe;

public partial class Form1 : Form
{
    private readonly TextBox _pathTextBox = new();
    private readonly Button _browseButton = new();
    private readonly Button _scanButton = new();
    private readonly Button _cancelScanButton = new();
    private readonly ComboBox _sortComboBox = new();
    private readonly TextBox _topGroupCountTextBox = new();
    private readonly Button _selectShowTopGroupsButton = new();
    private readonly Button _clearGroupFilterButton = new();
    private readonly Button _autoSelectButton = new();
    private readonly Button _clearSelectionButton = new();
    private readonly Button _deleteButton = new();
    private readonly Label _statusLabel = new();
    private readonly Label _statsLabel = new();
    private readonly Label _busyLabel = new();
    private readonly ProgressBar _busyProgressBar = new();
    private readonly DataGridView _grid = new();

    private readonly BindingList<DuplicateRow> _rows = new();
    private readonly List<DuplicateGroup> _groups = new();
    private readonly HashSet<string> _selectedPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, int> _groupToClusterId = new();
    private readonly Dictionary<int, string> _clusterMotherDirectory = new();
    private readonly Dictionary<int, Color> _clusterColors = new();
    private readonly Dictionary<int, string> _groupKeepPath = new();

    private static readonly Color[] ClusterPalette =
    [
        Color.FromArgb(255, 220, 240),
        Color.FromArgb(220, 235, 255),
        Color.FromArgb(220, 255, 230),
        Color.FromArgb(255, 238, 212),
        Color.FromArgb(236, 226, 255),
        Color.FromArgb(255, 224, 224),
        Color.FromArgb(220, 245, 245),
        Color.FromArgb(245, 235, 220)
    ];

    private bool _isScanning;
    private bool _isDeleting;
    private bool _suppressGridSelectionEvents;
    private CancellationTokenSource? _scanCts;
    private HashSet<int>? _visibleGroupIds;

    public Form1()
    {
        InitializeComponent();
        BuildUi();
        BindGrid();
    }

    private void BuildUi()
    {
        Text = "DupeXDupe - Visual Duplicate Explorer";
        Width = 1320;
        Height = 780;
        MinimumSize = new Size(1020, 620);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(10)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var scanRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 6,
            AutoSize = true
        };
        scanRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        scanRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        scanRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        scanRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        scanRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        scanRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        scanRow.Controls.Add(new Label { Text = "Scan Path", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);

        _pathTextBox.Dock = DockStyle.Fill;
        scanRow.Controls.Add(_pathTextBox, 1, 0);

        _browseButton.Text = "Browse";
        _browseButton.AutoSize = true;
        _browseButton.Click += (_, _) => BrowsePath();
        scanRow.Controls.Add(_browseButton, 2, 0);

        _scanButton.Text = "Scan";
        _scanButton.AutoSize = true;
        _scanButton.Click += async (_, _) => await StartScanAsync();
        scanRow.Controls.Add(_scanButton, 3, 0);

        _cancelScanButton.Text = "Cancel Scan";
        _cancelScanButton.AutoSize = true;
        _cancelScanButton.Enabled = false;
        _cancelScanButton.Click += (_, _) => CancelScan();
        scanRow.Controls.Add(_cancelScanButton, 4, 0);

        _statsLabel.Text = "No results";
        _statsLabel.AutoSize = true;
        _statsLabel.Anchor = AnchorStyles.Left;
        scanRow.Controls.Add(_statsLabel, 5, 0);

        var actionsRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };

        actionsRow.Controls.Add(new Label { Text = "Sort", AutoSize = true, Margin = new Padding(0, 8, 8, 0) });

        _sortComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _sortComboBox.Items.AddRange(new object[] { "Biggest", "Oldest", "Newest", "Name", "Group" });
        _sortComboBox.SelectedIndex = 0;
        _sortComboBox.SelectedIndexChanged += (_, _) => RefreshRows();
        actionsRow.Controls.Add(_sortComboBox);

        actionsRow.Controls.Add(new Label { Text = "Top Groups (0/all)", AutoSize = true, Margin = new Padding(12, 8, 8, 0) });

        _topGroupCountTextBox.Width = 80;
        _topGroupCountTextBox.Text = "100";
        actionsRow.Controls.Add(_topGroupCountTextBox);

        _selectShowTopGroupsButton.Text = "Select + Show Top N";
        _selectShowTopGroupsButton.AutoSize = true;
        _selectShowTopGroupsButton.Click += (_, _) => SelectAndShowTopGroups();
        actionsRow.Controls.Add(_selectShowTopGroupsButton);

        _clearGroupFilterButton.Text = "Show All Groups";
        _clearGroupFilterButton.AutoSize = true;
        _clearGroupFilterButton.Click += (_, _) => ClearGroupFilter();
        actionsRow.Controls.Add(_clearGroupFilterButton);

        _autoSelectButton.Text = "Auto-Select (Keep 1)";
        _autoSelectButton.AutoSize = true;
        _autoSelectButton.Click += (_, _) => AutoSelectKeepOne();
        actionsRow.Controls.Add(_autoSelectButton);

        _clearSelectionButton.Text = "Clear Selection";
        _clearSelectionButton.AutoSize = true;
        _clearSelectionButton.Click += (_, _) => ClearSelection();
        actionsRow.Controls.Add(_clearSelectionButton);

        _deleteButton.Text = "Delete Selected";
        _deleteButton.AutoSize = true;
        _deleteButton.Click += async (_, _) => await DeleteSelectedAsync();
        actionsRow.Controls.Add(_deleteButton);

        _grid.Dock = DockStyle.Fill;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AutoGenerateColumns = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = false;

        var statusRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };

        _statusLabel.Text = "Choose a drive/folder and click Scan";
        _statusLabel.AutoSize = true;
        statusRow.Controls.Add(_statusLabel);

        _busyProgressBar.Style = ProgressBarStyle.Marquee;
        _busyProgressBar.MarqueeAnimationSpeed = 25;
        _busyProgressBar.Visible = false;
        _busyProgressBar.Width = 140;
        _busyProgressBar.Height = 14;
        _busyProgressBar.Margin = new Padding(12, 6, 0, 0);
        statusRow.Controls.Add(_busyProgressBar);

        _busyLabel.AutoSize = true;
        _busyLabel.Visible = false;
        _busyLabel.Margin = new Padding(8, 4, 0, 0);
        statusRow.Controls.Add(_busyLabel);

        root.Controls.Add(scanRow, 0, 0);
        root.Controls.Add(actionsRow, 0, 1);
        root.Controls.Add(_grid, 0, 2);
        root.Controls.Add(statusRow, 0, 3);

        Controls.Add(root);
    }

    private void BindGrid()
    {
        _grid.Columns.Clear();
        _grid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            DataPropertyName = nameof(DuplicateRow.Selected),
            HeaderText = "Sel",
            Width = 45
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(DuplicateRow.GroupId),
            HeaderText = "Group",
            Width = 60
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(DuplicateRow.Name),
            HeaderText = "Name",
            Width = 240
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(DuplicateRow.SizeText),
            HeaderText = "Size",
            Width = 100
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(DuplicateRow.ModifiedText),
            HeaderText = "Modified",
            Width = 160
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(DuplicateRow.Path),
            HeaderText = "Path",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        });

        _grid.DataSource = _rows;
        _grid.CurrentCellDirtyStateChanged += Grid_CurrentCellDirtyStateChanged;
        _grid.CellValueChanged += Grid_CellValueChanged;
        _grid.DataBindingComplete += (_, _) => ApplyRowColors();
    }

    private void BrowsePath()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select a drive or folder to scan"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            _pathTextBox.Text = dialog.SelectedPath;
        }
    }

    private async Task StartScanAsync()
    {
        if (_isScanning)
        {
            MessageBox.Show("A scan is already running.", "Scan in progress", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var scanPath = _pathTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(scanPath) || !Directory.Exists(scanPath))
        {
            MessageBox.Show("Choose a valid drive or folder.", "Invalid path", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        SetScanningState(true);
        _rows.Clear();
        _groups.Clear();
        _selectedPaths.Clear();
        _groupToClusterId.Clear();
        _clusterMotherDirectory.Clear();
        _clusterColors.Clear();
        _groupKeepPath.Clear();
        _visibleGroupIds = null;
        _statsLabel.Text = "Scanning...";

        var progress = new Progress<string>(message => _statusLabel.Text = message);
        using var cts = new CancellationTokenSource();
        _scanCts = cts;

        try
        {
            var result = await DuplicateScanner.ScanAsync(scanPath, ScanMode.Fast, progress, cts.Token);
            _groups.AddRange(result.Groups);

            BuildClustersAndApplyDefaultMotherSelection();
            RefreshRows();
            UpdateStatsAndStatus($"Scan complete (size + name). Scanned {result.ScannedFiles:N0} files.");
        }
        catch (OperationCanceledException)
        {
            _statusLabel.Text = "Scan cancelled.";
            _statsLabel.Text = "No results";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "Scan failed.";
            MessageBox.Show(ex.Message, "Scan failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            if (ReferenceEquals(_scanCts, cts))
            {
                _scanCts = null;
            }

            SetScanningState(false);
        }
    }

    private void SetScanningState(bool scanning)
    {
        _isScanning = scanning;
        _scanButton.Enabled = !scanning && !_isDeleting;
        _browseButton.Enabled = !scanning && !_isDeleting;
        _sortComboBox.Enabled = !scanning && !_isDeleting;
        _selectShowTopGroupsButton.Enabled = !scanning && !_isDeleting;
        _clearGroupFilterButton.Enabled = !scanning && !_isDeleting;
        _deleteButton.Enabled = !scanning && !_isDeleting;
        _autoSelectButton.Enabled = !scanning && !_isDeleting;
        _clearSelectionButton.Enabled = !scanning && !_isDeleting;
        _cancelScanButton.Enabled = scanning;
    }

    private void SetDeleteBusyState(bool busy, string text)
    {
        _isDeleting = busy;
        _busyProgressBar.Visible = busy;
        _busyLabel.Visible = busy;
        _busyLabel.Text = text;
        _scanButton.Enabled = !_isScanning && !busy;
        _browseButton.Enabled = !_isScanning && !busy;
        _sortComboBox.Enabled = !_isScanning && !busy;
        _selectShowTopGroupsButton.Enabled = !_isScanning && !busy;
        _clearGroupFilterButton.Enabled = !_isScanning && !busy;
        _deleteButton.Enabled = !_isScanning && !busy;
        _autoSelectButton.Enabled = !_isScanning && !busy;
        _clearSelectionButton.Enabled = !_isScanning && !busy;
        _grid.Enabled = !busy;
    }

    private void CancelScan()
    {
        if (!_isScanning)
        {
            return;
        }

        _scanCts?.Cancel();
        _statusLabel.Text = "Cancelling scan...";
    }

    // Scan mode prompt removed: duplicate detection is now size + name only.
    private void RefreshRows()
    {
        var selectedSnapshot = new HashSet<string>(_selectedPaths, StringComparer.OrdinalIgnoreCase);

        var allRows = _groups
            .SelectMany(group => group.Files.Select(file => new DuplicateRow
            {
                ClusterId = _groupToClusterId.GetValueOrDefault(group.GroupId),
                GroupId = group.GroupId,
                Name = file.Name,
                Path = file.Path,
                Size = file.Size,
                SizeText = DuplicateScanner.FormatSize(file.Size),
                Modified = file.ModifiedUtc,
                ModifiedText = file.ModifiedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                Selected = selectedSnapshot.Contains(file.Path)
            }))
            .ToList();

        if (_visibleGroupIds is not null)
        {
            allRows = allRows.Where(r => _visibleGroupIds.Contains(r.GroupId)).ToList();
        }

        allRows = _sortComboBox.SelectedItem?.ToString() switch
        {
            "Oldest" => allRows.OrderBy(r => r.Modified).ToList(),
            "Newest" => allRows.OrderByDescending(r => r.Modified).ToList(),
            "Name" => allRows.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToList(),
            "Group" => allRows.OrderBy(r => r.GroupId).ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToList(),
            _ => allRows.OrderByDescending(r => r.Size).ToList(),
        };

        _suppressGridSelectionEvents = true;
        try
        {
            _rows.RaiseListChangedEvents = false;
            _rows.Clear();
            foreach (var row in allRows)
            {
                _rows.Add(row);
            }

            _rows.RaiseListChangedEvents = true;
            _rows.ResetBindings();
        }
        finally
        {
            _suppressGridSelectionEvents = false;
        }

        ApplyRowColors();
    }

    private void ApplyRowColors()
    {
        foreach (DataGridViewRow gridRow in _grid.Rows)
        {
            if (gridRow.DataBoundItem is not DuplicateRow row)
            {
                continue;
            }

            if (!_clusterColors.TryGetValue(row.ClusterId, out var clusterColor))
            {
                clusterColor = Color.White;
            }

            var background = row.Selected
                ? Blend(clusterColor, Color.White, 0.58f)
                : Blend(clusterColor, Color.White, 0.32f);

            gridRow.DefaultCellStyle.BackColor = background;
            gridRow.DefaultCellStyle.SelectionBackColor = Blend(clusterColor, Color.Black, 0.22f);
            gridRow.DefaultCellStyle.SelectionForeColor = Color.White;
        }
    }

    private static Color Blend(Color a, Color b, float t)
    {
        var clamped = Math.Clamp(t, 0f, 1f);
        var r = (int)Math.Round(a.R + (b.R - a.R) * clamped);
        var g = (int)Math.Round(a.G + (b.G - a.G) * clamped);
        var bl = (int)Math.Round(a.B + (b.B - a.B) * clamped);
        return Color.FromArgb(r, g, bl);
    }

    private void BuildClustersAndApplyDefaultMotherSelection()
    {
        _groupToClusterId.Clear();
        _clusterMotherDirectory.Clear();
        _clusterColors.Clear();
        _groupKeepPath.Clear();
        _selectedPaths.Clear();

        var signatureToClusterId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var clusterGroups = new Dictionary<int, List<DuplicateGroup>>();
        var nextClusterId = 1;

        foreach (var group in _groups)
        {
            var signature = BuildPathSignature(group);
            if (!signatureToClusterId.TryGetValue(signature, out var clusterId))
            {
                clusterId = nextClusterId++;
                signatureToClusterId[signature] = clusterId;
                clusterGroups[clusterId] = [];
            }

            _groupToClusterId[group.GroupId] = clusterId;
            clusterGroups[clusterId].Add(group);
        }

        foreach (var kvp in clusterGroups)
        {
            var clusterId = kvp.Key;
            var groups = kvp.Value;
            _clusterColors[clusterId] = ClusterPalette[(clusterId - 1) % ClusterPalette.Length];

            var motherDir = ChooseDefaultMotherDirectory(groups);
            _clusterMotherDirectory[clusterId] = motherDir;
            ApplyClusterMotherDirectory(clusterId, motherDir);
        }
    }

    private static string BuildPathSignature(DuplicateGroup group)
    {
        var dirs = group.Files
            .Select(file => NormalizeDirectory(file.Path))
            .Where(dir => !string.IsNullOrEmpty(dir))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(dir => dir, StringComparer.OrdinalIgnoreCase);

        return string.Join("|", dirs);
    }

    private static string ChooseDefaultMotherDirectory(List<DuplicateGroup> groups)
    {
        var stats = new Dictionary<string, (int Count, DateTime Latest)>(StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            foreach (var file in group.Files)
            {
                var dir = NormalizeDirectory(file.Path);
                if (string.IsNullOrEmpty(dir))
                {
                    continue;
                }

                if (!stats.TryGetValue(dir, out var current))
                {
                    stats[dir] = (1, file.ModifiedUtc);
                    continue;
                }

                var latest = file.ModifiedUtc > current.Latest ? file.ModifiedUtc : current.Latest;
                stats[dir] = (current.Count + 1, latest);
            }
        }

        return stats
            .OrderByDescending(x => x.Value.Count)
            .ThenByDescending(x => x.Value.Latest)
            .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Key)
            .FirstOrDefault() ?? string.Empty;
    }

    private void ApplyClusterMotherDirectory(int clusterId, string motherDirectory)
    {
        var groups = _groups.Where(g => _groupToClusterId.GetValueOrDefault(g.GroupId) == clusterId).ToList();

        foreach (var group in groups)
        {
            var keep = group.Files
                .FirstOrDefault(file => string.Equals(NormalizeDirectory(file.Path), motherDirectory, StringComparison.OrdinalIgnoreCase));

            keep ??= group.Files
                .OrderByDescending(f => f.ModifiedUtc)
                .ThenBy(f => f.Path, StringComparer.OrdinalIgnoreCase)
                .First();

            _groupKeepPath[group.GroupId] = keep.Path;

            foreach (var file in group.Files)
            {
                if (string.Equals(file.Path, keep.Path, StringComparison.OrdinalIgnoreCase))
                {
                    _selectedPaths.Remove(file.Path);
                }
                else
                {
                    _selectedPaths.Add(file.Path);
                }
            }
        }
    }

    private static string NormalizeDirectory(string path)
    {
        var directory = Path.GetDirectoryName(path);
        return string.IsNullOrWhiteSpace(directory)
            ? string.Empty
            : directory.TrimEnd(Path.DirectorySeparatorChar).ToLowerInvariant();
    }

    private void AutoSelectKeepOne()
    {
        _selectedPaths.Clear();

        foreach (var group in _groups)
        {
            var keep = group.Files
                .OrderByDescending(f => f.ModifiedUtc)
                .ThenBy(f => f.Path, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (keep is null)
            {
                continue;
            }

            _groupKeepPath[group.GroupId] = keep.Path;
            var clusterId = _groupToClusterId.GetValueOrDefault(group.GroupId);
            if (clusterId > 0)
            {
                _clusterMotherDirectory[clusterId] = NormalizeDirectory(keep.Path);
            }

            foreach (var file in group.Files)
            {
                if (!string.Equals(file.Path, keep.Path, StringComparison.OrdinalIgnoreCase))
                {
                    _selectedPaths.Add(file.Path);
                }
            }
        }

        RefreshRows();
        _statusLabel.Text = "Auto-selected duplicates (keeping newest per group).";
    }

    private void ClearSelection()
    {
        _selectedPaths.Clear();
        RefreshRows();
        _statusLabel.Text = "Cleared delete selection.";
    }
    private async Task DeleteSelectedAsync()
    {
        var selectedRows = _groups
            .SelectMany(g => g.Files.Select(file => new DuplicateRow
            {
                ClusterId = _groupToClusterId.GetValueOrDefault(g.GroupId),
                GroupId = g.GroupId,
                Name = file.Name,
                Path = file.Path,
                Size = file.Size,
                Modified = file.ModifiedUtc
            }))
            .Where(r => _selectedPaths.Contains(r.Path))
            .ToList();

        if (selectedRows.Count == 0)
        {
            MessageBox.Show("Select duplicate rows to delete.", "Nothing selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var rowsToDelete = selectedRows;
        var totalBytes = rowsToDelete.Sum(r => r.Size);
        var prompt = $"Move {rowsToDelete.Count:N0} files to Recycle Bin and free {DuplicateScanner.FormatSize(totalBytes)}?";

        var confirm = MessageBox.Show(
            prompt,
            "Confirm delete",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm != DialogResult.Yes)
        {
            return;
        }

        SetDeleteBusyState(true, "Deleting...");
        _statusLabel.Text = $"Deleting {rowsToDelete.Count:N0} files...";

        var progress = new Progress<(int done, int total, string path)>(p =>
        {
            _busyLabel.Text = $"Deleting {p.done:N0}/{p.total:N0}";
            _statusLabel.Text = $"Deleting {p.done:N0}/{p.total:N0}: {Path.GetFileName(p.path)}";
        });

        DeleteResult deleteResult;
        try
        {
            deleteResult = await Task.Run(() => DeleteFilesToRecycleBin(rowsToDelete, progress));
        }
        finally
        {
            SetDeleteBusyState(false, string.Empty);
        }

        var deletedPaths = deleteResult.DeletedPaths;
        var errors = deleteResult.Errors;

        var newGroups = new List<DuplicateGroup>();
        foreach (var group in _groups)
        {
            var kept = group.Files
                .Where(f => !deletedPaths.Contains(f.Path))
                .ToList();

            if (kept.Count > 1)
            {
                newGroups.Add(new DuplicateGroup(group.GroupId, kept));
            }
        }

        _groups.Clear();
        _groups.AddRange(newGroups.Select((g, idx) => new DuplicateGroup(idx + 1, g.Files)));
        if (_visibleGroupIds is not null)
        {
            _visibleGroupIds = _groups.Select(g => g.GroupId).ToHashSet();
        }

        BuildClustersAndApplyDefaultMotherSelection();
        RefreshRows();

        if (errors.Count > 0)
        {
            _statusLabel.Text = $"Deleted {deletedPaths.Count:N0} files. Failed to delete {errors.Count:N0}.";
            MessageBox.Show(string.Join(Environment.NewLine, errors.Take(15)), "Some files failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _statusLabel.Text = $"Deleted {deletedPaths.Count:N0} files.";
        UpdateStatsOnly();
    }

    private static DeleteResult DeleteFilesToRecycleBin(
        List<DuplicateRow> rowsToDelete,
        IProgress<(int done, int total, string path)> progress)
    {
        var deletedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<string>();
        var done = 0;
        var total = rowsToDelete.Count;

        foreach (var row in rowsToDelete)
        {
            try
            {
                FileSystem.DeleteFile(
                    row.Path,
                    UIOption.OnlyErrorDialogs,
                    RecycleOption.SendToRecycleBin);
                deletedPaths.Add(row.Path);
            }
            catch (Exception ex)
            {
                errors.Add($"{row.Path} ({ex.Message})");
            }

            done++;
            progress.Report((done, total, row.Path));
        }

        return new DeleteResult(deletedPaths, errors);
    }

    private void SelectAndShowTopGroups()
    {
        if (_groups.Count == 0)
        {
            MessageBox.Show("Run a scan first.", "No results", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var requestedCount = ParseTopGroupCount(_topGroupCountTextBox.Text, _groups.Count);
        var orderedGroups = GetGroupsByCurrentSort();
        var chosen = orderedGroups.Take(requestedCount).ToList();
        if (chosen.Count == 0)
        {
            MessageBox.Show("No groups available for that count.", "No groups", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _visibleGroupIds = chosen.Select(g => g.GroupId).ToHashSet();
        _selectedPaths.Clear();

        foreach (var group in chosen)
        {
            var keepPath = _groupKeepPath.GetValueOrDefault(group.GroupId)
                           ?? group.Files.OrderByDescending(f => f.ModifiedUtc).First().Path;

            foreach (var file in group.Files)
            {
                if (!string.Equals(file.Path, keepPath, StringComparison.OrdinalIgnoreCase))
                {
                    _selectedPaths.Add(file.Path);
                }
            }
        }

        RefreshRows();
        _statusLabel.Text = $"Showing top {chosen.Count:N0} groups with current mother-path rules.";
    }

    private static int ParseTopGroupCount(string text, int maxCount)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return maxCount;
        }

        if (!int.TryParse(text.Trim(), out var value) || value <= 0)
        {
            return maxCount;
        }

        return Math.Min(value, maxCount);
    }

    private void ClearGroupFilter()
    {
        _visibleGroupIds = null;
        RefreshRows();
        _statusLabel.Text = "Showing all groups from current scan.";
    }

    private List<DuplicateGroup> GetGroupsByCurrentSort()
    {
        return _sortComboBox.SelectedItem?.ToString() switch
        {
            "Oldest" => _groups.OrderBy(g => g.Files.Min(f => f.ModifiedUtc)).ToList(),
            "Newest" => _groups.OrderByDescending(g => g.Files.Max(f => f.ModifiedUtc)).ToList(),
            "Name" => _groups.OrderBy(g => g.Files.Min(f => f.Name), StringComparer.OrdinalIgnoreCase).ToList(),
            "Group" => _groups.OrderBy(g => g.GroupId).ToList(),
            _ => _groups.OrderByDescending(g => g.Files.FirstOrDefault()?.Size ?? 0).ToList(),
        };
    }
    private void Grid_CurrentCellDirtyStateChanged(object? sender, EventArgs e)
    {
        if (_grid.IsCurrentCellDirty)
        {
            _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }
    }

    private void Grid_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex != 0 || _suppressGridSelectionEvents)
        {
            return;
        }

        if (_grid.Rows[e.RowIndex].DataBoundItem is not DuplicateRow row)
        {
            return;
        }

        if (row.Selected)
        {
            _selectedPaths.Add(row.Path);
        }
        else
        {
            _selectedPaths.Remove(row.Path);
        }

        ApplyUserMotherChoice(row);
        RefreshRows();
    }

    private void ApplyUserMotherChoice(DuplicateRow editedRow)
    {
        var group = _groups.FirstOrDefault(g => g.GroupId == editedRow.GroupId);
        if (group is null)
        {
            return;
        }

        var clusterId = _groupToClusterId.GetValueOrDefault(group.GroupId);
        if (clusterId <= 0)
        {
            return;
        }

        var keepPath = ResolveKeepPathAfterEdit(group, editedRow);
        if (string.IsNullOrEmpty(keepPath))
        {
            return;
        }

        var newMotherDirectory = NormalizeDirectory(keepPath);
        _clusterMotherDirectory[clusterId] = newMotherDirectory;
        ApplyClusterMotherDirectory(clusterId, newMotherDirectory);

        var changedGroups = _groups.Count(g => _groupToClusterId.GetValueOrDefault(g.GroupId) == clusterId);
        _statusLabel.Text =
            $"Cluster {clusterId}: mother path set to '{newMotherDirectory}'. Updated {changedGroups:N0} groups in that cluster.";
    }

    private string ResolveKeepPathAfterEdit(DuplicateGroup group, DuplicateRow editedRow)
    {
        var unselected = group.Files
            .Where(file => !_selectedPaths.Contains(file.Path))
            .ToList();

        if (editedRow.Selected == false)
        {
            return editedRow.Path;
        }

        if (unselected.Count > 0)
        {
            return unselected[0].Path;
        }

        var clusterId = _groupToClusterId.GetValueOrDefault(group.GroupId);
        var motherDirectory = _clusterMotherDirectory.GetValueOrDefault(clusterId);

        var fallback = group.Files
            .FirstOrDefault(file =>
                !string.Equals(file.Path, editedRow.Path, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(NormalizeDirectory(file.Path), motherDirectory, StringComparison.OrdinalIgnoreCase))
            ?? group.Files
                .FirstOrDefault(file => !string.Equals(file.Path, editedRow.Path, StringComparison.OrdinalIgnoreCase))
            ?? group.Files.First();

        return fallback.Path;
    }

    private void UpdateStatsAndStatus(string statusPrefix)
    {
        var duplicateFileCount = _groups.Sum(g => g.Files.Count);
        var reclaimableBytes = _groups.Sum(g => g.Files[0].Size * (g.Files.Count - 1L));
        _statusLabel.Text =
            $"{statusPrefix} Found {_groups.Count:N0} duplicate groups ({duplicateFileCount:N0} files).";
        _statsLabel.Text =
            $"Groups: {_groups.Count:N0} | Duplicate files: {duplicateFileCount:N0} | Reclaimable: {DuplicateScanner.FormatSize(reclaimableBytes)}";
    }

    private void UpdateStatsOnly()
    {
        var duplicateFileCount = _groups.Sum(g => g.Files.Count);
        var reclaimableBytes = _groups.Sum(g => g.Files[0].Size * (g.Files.Count - 1L));
        _statsLabel.Text =
            $"Groups: {_groups.Count:N0} | Duplicate files: {duplicateFileCount:N0} | Reclaimable: {DuplicateScanner.FormatSize(reclaimableBytes)}";
    }
}

public sealed record DeleteResult(HashSet<string> DeletedPaths, List<string> Errors);

public sealed class DuplicateRow
{
    public bool Selected { get; set; }
    public int ClusterId { get; set; }
    public int GroupId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SizeText { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime Modified { get; set; }
    public string ModifiedText { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
}

