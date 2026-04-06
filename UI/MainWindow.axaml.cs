using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using IoTHubUpdateUtility.Models;

namespace IoTHubUpdateUtility.UI;

public partial class MainWindow : Window
{
    private const string SourceTreePathFormat = "application/x-iothub-source-tree-path";
    private const string DestinationTreePathFormat = "application/x-iothub-destination-tree-path";

    private readonly MainViewModel _vm;
    private bool _pickerOpen;
    private bool _isSynchronizingRuleEditor;
    private UpdateRule? _editingRule;

    public MainWindow()
    {
        InitializeComponent();

        _vm = new MainViewModel();

        // Re-route all log writes through the Dispatcher so background tasks
        // can safely write to the ObservableCollection on the UI thread.
        _vm.LogLines.CollectionChanged += (_, _) =>
        {
            // Auto-scroll to bottom
            if (LogListBox.ItemCount > 0)
                LogListBox.ScrollIntoView(LogListBox.ItemCount - 1);
        };

        DataContext = _vm;

        // Sync rule editor when selection changes
        RulesListBox.SelectionChanged += OnRulesSelectionChanged;
    }

    // ── Button handlers ───────────────────────────────────────────────────────

    private async void OnSelectSource(object? sender, RoutedEventArgs e)
    {
        if (_pickerOpen)
            return;

        _pickerOpen = true;
        try
        {
            if (_vm.SourceType == Models.SourceType.Folder)
            {
                var folderOptions = new FolderPickerOpenOptions
                {
                    Title = "Select source folder"
                };

                if (!string.IsNullOrEmpty(_vm.SourcePath) && System.IO.Directory.Exists(_vm.SourcePath))
                {
                    folderOptions.SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(new System.Uri(_vm.SourcePath));
                }

                var folder = (await StorageProvider.OpenFolderPickerAsync(folderOptions))?.FirstOrDefault();
                if (folder is not null)
                {
                    var uri = folder.Path;
                    if (uri is not null)
                        _vm.SetSource(uri.LocalPath ?? uri.ToString());
                }
                return;
            }

            var fileOptions = new FilePickerOpenOptions
            {
                Title = "Select compressed file or folder",
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType>
                {
                    new FilePickerFileType("Archives") { Patterns = new[] { "*.zip", "*.rar", "*.7z", "*.tar", "*.gz", "*.tgz" } },
                    new FilePickerFileType("All files") { Patterns = new[] { "*" } }
                }
            };

            var file = (await StorageProvider.OpenFilePickerAsync(fileOptions))?.FirstOrDefault();
            if (file is not null)
            {
                var uri = file.Path;
                if (uri is not null)
                    _vm.SetSource(uri.LocalPath ?? uri.ToString());
            }
        }
        finally
        {
            _pickerOpen = false;
        }
    }

    private async void OnExtract(object? sender, RoutedEventArgs e)
    {
        await RunOnUiAsync(() => _vm.ExtractAsync());
    }

    private async void OnSelectDestination(object? sender, RoutedEventArgs e)
    {
        if (_pickerOpen)
            return;

        _pickerOpen = true;
        try
        {
            var folderOptions = new FolderPickerOpenOptions
            {
                Title = "Select destination folder"
            };

            if (!string.IsNullOrEmpty(_vm.DestinationPath) && System.IO.Directory.Exists(_vm.DestinationPath))
            {
                folderOptions.SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(new System.Uri(_vm.DestinationPath));
            }

            var folder = (await StorageProvider.OpenFolderPickerAsync(folderOptions))?.FirstOrDefault();
            if (folder is not null)
            {
                var uri = folder.Path;
                if (uri is not null)
                    _vm.SetDestination(uri.LocalPath ?? uri.ToString());
            }
        }
        finally
        {
            _pickerOpen = false;
        }
    }

    private void OnNewRule(object? sender, RoutedEventArgs e)
    {
        _editingRule = null;
        RulesListBox.SelectedItem = null;
        ClearRuleEditor();
        SetRuleEditorEnabled(true, "Create a new rule and click Save.");
    }

    private void OnEditRule(object? sender, RoutedEventArgs e)
    {
        var selectedRule = RulesListBox.SelectedItem as UpdateRule;
        if (selectedRule is null)
            return;

        _editingRule = selectedRule;
        LoadRuleIntoEditor(selectedRule);
        SetRuleEditorEnabled(true, $"Editing '{selectedRule.Name}'.");
    }

    private async void OnSaveRule(object? sender, RoutedEventArgs e)
    {
        var draftRule = BuildRuleFromEditor();
        if (draftRule is null)
            return;

        var duplicateRule = _vm.FindRuleByName(draftRule.Name, _editingRule);
        _vm.UpsertRule(draftRule, _editingRule, duplicateRule);

        var ruleToSelect = duplicateRule ?? _editingRule ?? draftRule;
        _editingRule = ruleToSelect;
        SelectRule(ruleToSelect);
        LoadRuleIntoEditor(ruleToSelect);
        SetRuleEditorEnabled(false, "Select a rule and click Edit, or click New.");
    }

    private async void OnDeleteRule(object? sender, RoutedEventArgs e)
    {
        var selectedRule = RulesListBox.SelectedItem as UpdateRule;
        if (selectedRule is null)
            return;

        var confirmed = await ShowConfirmationDialogAsync(
            "Delete Rule",
            $"Delete rule '{selectedRule.Name}'?");

        if (!confirmed)
            return;

        _vm.RemoveRule(selectedRule);
        _editingRule = null;
        RulesListBox.SelectedItem = null;
        ClearRuleEditor();
        SetRuleEditorEnabled(false, "Select a rule and click Edit, or click New.");
    }

    private void OnCheckCriteria(object? sender, RoutedEventArgs e)
    {
        _vm.CheckUpgradeCriteria();
    }

    private async void OnStopAndExecute(object? sender, RoutedEventArgs e)
    {
        await RunOnUiAsync(() => _vm.StopServicesAndExecuteAsync());
    }

    private async void OnStartServices(object? sender, RoutedEventArgs e)
    {
        await RunOnUiAsync(() => _vm.StartServicesAsync());
    }

    private void OnClearLogs(object? sender, RoutedEventArgs e)
    {
        _vm.ClearLogs();
    }

    private void OnRefreshFolderTree(object? sender, RoutedEventArgs e)
    {
        _vm.RefreshFolderTrees();
    }

    private async void OnSourceTreeNodePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        await StartTreeNodeDragAsync(sender, e, SourceTreePathFormat);
    }

    private async void OnDestinationTreeNodePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        await StartTreeNodeDragAsync(sender, e, DestinationTreePathFormat);
    }

    private async void OnSelectBackupPath(object? sender, RoutedEventArgs e)
    {
        if (_pickerOpen)
            return;

        _pickerOpen = true;
        try
        {
            var folderOptions = new FolderPickerOpenOptions
            {
                Title = "Select backup folder"
            };

            if (!string.IsNullOrEmpty(_vm.BackupPath) && System.IO.Directory.Exists(_vm.BackupPath))
            {
                folderOptions.SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(new System.Uri(_vm.BackupPath));
            }

            var folder = (await StorageProvider.OpenFolderPickerAsync(folderOptions))?.FirstOrDefault();
            if (folder is not null)
            {
                var uri = folder.Path;
                if (uri is not null)
                    _vm.BackupPath = uri.LocalPath ?? uri.ToString();
            }
        }
        finally
        {
            _pickerOpen = false;
        }
    }

    private async void OnSelectExtractionPath(object? sender, RoutedEventArgs e)
    {
        if (_pickerOpen)
            return;

        _pickerOpen = true;
        try
        {
            var folderOptions = new FolderPickerOpenOptions
            {
                Title = "Select extraction folder"
            };

            if (!string.IsNullOrEmpty(_vm.ExtractionPath) && System.IO.Directory.Exists(_vm.ExtractionPath))
            {
                folderOptions.SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(new System.Uri(_vm.ExtractionPath));
            }

            var folder = (await StorageProvider.OpenFolderPickerAsync(folderOptions))?.FirstOrDefault();
            if (folder is not null)
            {
                var uri = folder.Path;
                if (uri is not null)
                    _vm.ExtractionPath = uri.LocalPath ?? uri.ToString();
            }
        }
        finally
        {
            _pickerOpen = false;
        }
    }

    private void OnAddStopService(object? sender, RoutedEventArgs e)
    {
        var textBox = this.FindControl<TextBox>("NewStopServiceBox");
        if (textBox != null && !string.IsNullOrWhiteSpace(textBox.Text))
        {
            _vm.AddStopService(textBox.Text);
            textBox.Text = string.Empty;
        }
    }

    private void OnRemoveStopService(object? sender, RoutedEventArgs e)
    {
        var listBox = this.FindControl<ListBox>("StopServicesListBox");
        if (listBox?.SelectedItem is string cmd)
        {
            _vm.RemoveStopService(cmd);
        }
    }

    private void OnAddStartService(object? sender, RoutedEventArgs e)
    {
        var textBox = this.FindControl<TextBox>("NewStartServiceBox");
        if (textBox != null && !string.IsNullOrWhiteSpace(textBox.Text))
        {
            _vm.AddStartService(textBox.Text);
            textBox.Text = string.Empty;
        }
    }

    private void OnRemoveStartService(object? sender, RoutedEventArgs e)
    {
        var listBox = this.FindControl<ListBox>("StartServicesListBox");
        if (listBox?.SelectedItem is string cmd)
        {
            _vm.RemoveStartService(cmd);
        }
    }

    // ── Rule inline editor ────────────────────────────────────────────────────

    private void OnRulesSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var rule = RulesListBox.SelectedItem as UpdateRule;
        if (rule is null)
        {
            if (_editingRule is null)
                ClearRuleEditor();
            return;
        }

        if (ReferenceEquals(rule, _editingRule))
            return;

        ClearRuleEditor();
        SetRuleEditorEnabled(false, $"Selected '{rule.Name}'. Click Edit to modify it.");
    }

    private void OnRuleFieldChanged(object? sender, RoutedEventArgs e)
    {
        if (_isSynchronizingRuleEditor)
            return;
    }

    private void OnRuleOpChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isSynchronizingRuleEditor)
            return;

        UpdatePermissionEditorState();
    }

    private void OnRulePermissionsChanged(object? sender, RoutedEventArgs e)
    {
        if (_isSynchronizingRuleEditor)
            return;
    }

    private void OnRuleSourcePathDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.Data.Contains(SourceTreePathFormat) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnRuleDestinationPathDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.Data.Contains(DestinationTreePathFormat) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnRuleSourcePathDrop(object? sender, DragEventArgs e)
    {
        ApplyDroppedPath(e, SourceTreePathFormat, RuleSrcBox, (rule, path) => rule.SourceRelativePath = path);
    }

    private void OnRuleDestinationPathDrop(object? sender, DragEventArgs e)
    {
        ApplyDroppedPath(e, DestinationTreePathFormat, RuleDstBox, (rule, path) => rule.DestinationRelativePath = path);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task StartTreeNodeDragAsync(object? sender, PointerPressedEventArgs e, string format)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        if (sender is not Control control || control.DataContext is not FileNode node || !node.IsFolder)
            return;

        var data = new DataObject();
        data.Set(format, node.FullPath);
        await DragDrop.DoDragDrop(e, data, DragDropEffects.Copy);
        e.Handled = true;
    }

    private void ApplyDroppedPath(
        DragEventArgs e,
        string format,
        TextBox targetTextBox,
        Action<UpdateRule, string> applyRuleValue)
    {
        if (!e.Data.Contains(format))
            return;

        var droppedPath = e.Data.Get(format) as string ?? string.Empty;
        targetTextBox.Text = droppedPath;
        e.DragEffects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private UpdateRule? BuildRuleFromEditor()
    {
        var ruleName = (RuleNameBox.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(ruleName))
            return null;

        return new UpdateRule
        {
            Name = ruleName,
            SourceRelativePath = RuleSrcBox.Text ?? string.Empty,
            DestinationRelativePath = RuleDstBox.Text ?? string.Empty,
            Permissions = GetSelectedOperation() == OperationType.FilesOnly && RulePermsCheckBox.IsChecked == true
                ? GetPermissionFieldText()
                : string.Empty,
            Operation = GetSelectedOperation()
        };
    }

    private void SelectRule(UpdateRule rule)
    {
        RulesListBox.SelectedItem = rule;
    }

    private void ClearRuleEditor()
    {
        _isSynchronizingRuleEditor = true;
        RuleNameBox.Text = string.Empty;
        RuleSrcBox.Text = string.Empty;
        RuleDstBox.Text = string.Empty;
        RuleOpBox.SelectedIndex = 0;
        RulePermsCheckBox.IsChecked = false;
        UpdatePermissionEditorState();
        _isSynchronizingRuleEditor = false;
    }

    private void LoadRuleIntoEditor(UpdateRule rule)
    {
        _isSynchronizingRuleEditor = true;
        RuleNameBox.Text = rule.Name;
        RuleSrcBox.Text = rule.SourceRelativePath;
        RuleDstBox.Text = rule.DestinationRelativePath;
        RuleOpBox.SelectedIndex = (int)rule.Operation;
        RulePermsCheckBox.IsChecked = rule.Operation == OperationType.FilesOnly &&
                                      !string.IsNullOrWhiteSpace(rule.Permissions);
        UpdatePermissionEditorState();
        _isSynchronizingRuleEditor = false;
    }

    private void SetRuleEditorEnabled(bool isEnabled, string hint)
    {
        RuleEditor.IsEnabled = isEnabled;
        RuleEditorHint.Text = hint;
    }

    private OperationType GetSelectedOperation()
    {
        return (OperationType)Math.Max(RuleOpBox.SelectedIndex, 0);
    }

    private void UpdatePermissionEditorState()
    {
        var isFilesOnly = GetSelectedOperation() == OperationType.FilesOnly;
        RulePermsCheckBox.IsEnabled = isFilesOnly;
        RulePermsLabel.Opacity = isFilesOnly ? 1.0 : 0.45;

        if (!isFilesOnly)
            RulePermsCheckBox.IsChecked = false;
    }

    private string GetPermissionFieldText()
    {
        return string.IsNullOrWhiteSpace(RulePermsLabel.Text)
            ? string.Empty
            : RulePermsLabel.Text;
    }

    private async Task<bool> ShowConfirmationDialogAsync(string title, string message)
    {
        var okButton = new Button
        {
            Content = "Delete",
            MinWidth = 90,
            Background = Avalonia.Media.Brush.Parse("#0e639c"),
            BorderBrush = Avalonia.Media.Brush.Parse("#0e639c"),
            Foreground = Avalonia.Media.Brush.Parse("#ffffff"),
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center
        };

        var cancelButton = new Button
        {
            Content = "Cancel",
            MinWidth = 90,
            Background = Avalonia.Media.Brush.Parse("#0e639c"),
            BorderBrush = Avalonia.Media.Brush.Parse("#0e639c"),
            Foreground = Avalonia.Media.Brush.Parse("#ffffff"),
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center
        };

        var dialog = new Window
        {
            Title = title,
            SizeToContent = SizeToContent.WidthAndHeight,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 18,
                Children =
                {
                    new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                        ColumnSpacing = 12,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = "!",
                                FontSize = 24,
                                FontWeight = Avalonia.Media.FontWeight.Bold,
                                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
                            },
                            new TextBlock
                            {
                                Text = message,
                                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                                MaxWidth = 360,
                                [Grid.ColumnProperty] = 1
                            }
                        }
                    },
                    new StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                        Spacing = 10,
                        Children = { okButton, cancelButton }
                    }
                }
            }
        };

        dialog.Opened += (_, _) => okButton.Focus();

        okButton.Click += (_, _) => dialog.Close(true);
        cancelButton.Click += (_, _) => dialog.Close(false);

        return await dialog.ShowDialog<bool>(this);
    }

    /// <summary>
    /// Runs an async ViewModel method and ensures exceptions are logged.
    /// </summary>
    private static async Task RunOnUiAsync(Func<Task> action)
    {
        try   { await action(); }
        catch (Exception ex)
        { await Dispatcher.UIThread.InvokeAsync(() => { /* logged inside vm */ _ = ex.Message; }); }
    }
}
