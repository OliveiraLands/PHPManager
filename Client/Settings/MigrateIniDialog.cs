//-----------------------------------------------------------------------
// <copyright>
// Copyright (C) Ruslan Yakushev for the PHP Manager for IIS project.
//
// This file is subject to the terms and conditions of the Microsoft Public License (MS-PL).
// See http://www.microsoft.com/opensource/licenses.mspx#Ms-PL for more details.
// </copyright>
//-----------------------------------------------------------------------

//#define VSDesigner

using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Forms;
using Microsoft.Web.Management.Client.Win32;
using Web.Management.PHP.Config;

namespace Web.Management.PHP.Settings
{

    /// <summary>
    /// Copies php.ini settings and enabled extensions from another registered PHP version
    /// to the php.ini of the active PHP version.
    /// </summary>
    internal sealed class MigrateIniDialog :
#if VSDesigner
        Form
#else
        TaskForm
#endif
    {
        private PHPModule _module;

        private ManagementPanel _contentPanel;
        private Label _sourceLabel;
        private ComboBox _sourceComboBox;
        private Label _targetLabel;
        private ListView _itemsListView;
        private Label _messageLabel;

        private string _activeHandlerName;
        private string _sourceHandlerName;
        private RemoteObjectCollection<PHPIniMigrationItem> _items;
        private bool _busy;

        public MigrateIniDialog(PHPModule module) : base(module)
        {
            _module = module;
            InitializeComponent();
            InitializeUI();
        }

        protected override bool CanAccept
        {
            get
            {
                return !_busy && _itemsListView.CheckedItems.Count > 0;
            }
        }

        protected override bool CanShowHelp
        {
            get
            {
                return false;
            }
        }

        private static string GetStatusText(PHPIniMigrationItem item)
        {
            switch (item.Status)
            {
                case PHPIniMigrationStatus.AvailableFromPECL:
                    PECLPackage package = item.PECLPackage;
                    return String.Format(CultureInfo.CurrentCulture, Resources.MigrateIniStatusAvailableFromPECL,
                        (package != null) ? package.Name + " " + package.Version : String.Empty);
                case PHPIniMigrationStatus.Changed:
                    return Resources.MigrateIniStatusChanged;
                case PHPIniMigrationStatus.Unsupported:
                    return Resources.MigrateIniStatusUnsupported;
                case PHPIniMigrationStatus.MissingExtension:
                    return Resources.MigrateIniStatusMissingExtension;
                default:
                    return Resources.MigrateIniStatusNew;
            }
        }

        private void InitializeComponent()
        {
            this._sourceLabel = new Label();
            this._sourceComboBox = new ComboBox();
            this._targetLabel = new Label();
            this._itemsListView = new ListView();
            this._messageLabel = new Label();
            this.SuspendLayout();

            this._sourceLabel.AutoSize = true;
            this._sourceLabel.Location = new System.Drawing.Point(0, 8);
            this._sourceLabel.Text = Resources.MigrateIniDialogSource;

            this._sourceComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            this._sourceComboBox.Location = new System.Drawing.Point(0, 25);
            this._sourceComboBox.Size = new System.Drawing.Size(560, 21);
            this._sourceComboBox.SelectedIndexChanged += new EventHandler(OnSourceComboBoxSelectedIndexChanged);

            this._targetLabel.AutoSize = true;
            this._targetLabel.Location = new System.Drawing.Point(0, 55);

            this._itemsListView.Location = new System.Drawing.Point(0, 75);
            this._itemsListView.Size = new System.Drawing.Size(560, 250);
            this._itemsListView.View = View.Details;
            this._itemsListView.CheckBoxes = true;
            this._itemsListView.FullRowSelect = true;
            this._itemsListView.Columns.Add(Resources.MigrateIniDialogColumnName, 170);
            this._itemsListView.Columns.Add(Resources.MigrateIniDialogColumnNewValue, 150);
            this._itemsListView.Columns.Add(Resources.MigrateIniDialogColumnCurrentValue, 110);
            this._itemsListView.Columns.Add(Resources.MigrateIniDialogColumnStatus, 120);
            this._itemsListView.ItemCheck += new ItemCheckEventHandler(OnItemsListViewItemCheck);
            this._itemsListView.ItemChecked += new ItemCheckedEventHandler(OnItemsListViewItemChecked);

            this._messageLabel.Location = new System.Drawing.Point(0, 330);
            this._messageLabel.Size = new System.Drawing.Size(560, 30);

            this.ClientSize = new System.Drawing.Size(600, 450);
            this.Name = "MigrateIniDialog";
            this.ResumeLayout(false);
#if VSDesigner
            this.PerformLayout();
#endif
        }

        private void InitializeUI()
        {
            _contentPanel = new ManagementPanel();
            _contentPanel.SuspendLayout();

            _contentPanel.Location = new System.Drawing.Point(0, 0);
            _contentPanel.Dock = DockStyle.Fill;
            _contentPanel.Controls.Add(_sourceLabel);
            _contentPanel.Controls.Add(_sourceComboBox);
            _contentPanel.Controls.Add(_targetLabel);
            _contentPanel.Controls.Add(_itemsListView);
            _contentPanel.Controls.Add(_messageLabel);

            _contentPanel.ResumeLayout(false);
            _contentPanel.PerformLayout();

            this.Text = Resources.MigrateIniDialogTitle;

            SetContent(_contentPanel);
            UpdateTaskForm();
        }

        protected override void OnAccept()
        {
            foreach (ListViewItem listItem in _itemsListView.Items)
            {
                ((PHPIniMigrationItem)listItem.Tag).Selected = listItem.Checked;
            }
            _sourceHandlerName = ((PHPVersion)_sourceComboBox.SelectedItem).HandlerName;

            SetBusy(true);
            StartAsyncTask(OnApplyDoWork, OnApplyCompleted);
        }

        private void OnApplyDoWork(object sender, DoWorkEventArgs e)
        {
            _module.Proxy.ApplyIniMigration(_sourceHandlerName, _items);
        }

        private void OnApplyCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            SetBusy(false);
            if (e.Error != null)
            {
                DisplayErrorMessage(e.Error, Resources.ResourceManager);
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private void OnGetPreviewDoWork(object sender, DoWorkEventArgs e)
        {
            e.Result = _module.Proxy.GetIniMigrationPreview((string)e.Argument);
        }

        private void OnGetPreviewCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            SetBusy(false);
            _itemsListView.BeginUpdate();
            _itemsListView.Items.Clear();
            try
            {
                if (e.Error != null)
                {
                    throw e.Error;
                }

                _items = (RemoteObjectCollection<PHPIniMigrationItem>)e.Result;
                foreach (PHPIniMigrationItem item in _items)
                {
                    string name = (item.Kind == PHPIniMigrationKind.Extension)
                        ? Resources.MigrateIniDialogExtension + ": " + item.Name
                        : item.Name;
                    ListViewItem listItem = new ListViewItem(name);
                    listItem.SubItems.Add(item.SourceValue);
                    listItem.SubItems.Add(item.TargetValue);
                    listItem.SubItems.Add(GetStatusText(item));
                    listItem.Tag = item;
                    listItem.Checked = item.Selected && PHPIniMigrationStatus.CanBeCopied(item.Status);
                    if (!PHPIniMigrationStatus.CanBeCopied(item.Status))
                    {
                        listItem.ForeColor = System.Drawing.SystemColors.GrayText;
                    }
                    _itemsListView.Items.Add(listItem);
                }

                _messageLabel.Text = (_items.Count == 0) ? Resources.MigrateIniDialogNothingToCopy : String.Empty;
            }
            catch (Exception ex)
            {
                DisplayErrorMessage(ex, Resources.ResourceManager);
            }
            finally
            {
                _itemsListView.EndUpdate();
            }

            UpdateTaskForm();
        }

        private void OnGetVersionsDoWork(object sender, DoWorkEventArgs e)
        {
            PHPConfigInfo configInfo = _module.Proxy.GetPHPConfigInfo();
            RemoteObjectCollection<PHPVersion> versions = _module.Proxy.GetAllPHPVersions();
            e.Result = new object[] { configInfo, versions };
        }

        private void OnGetVersionsCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            SetBusy(false);
            try
            {
                if (e.Error != null)
                {
                    throw e.Error;
                }

                object[] result = (object[])e.Result;
                PHPConfigInfo configInfo = (PHPConfigInfo)result[0];
                RemoteObjectCollection<PHPVersion> versions = (RemoteObjectCollection<PHPVersion>)result[1];

                _activeHandlerName = configInfo.HandlerName;
                _targetLabel.Text = String.Format(CultureInfo.CurrentCulture, Resources.MigrateIniDialogTarget,
                    configInfo.Version + " (" + configInfo.Executable + ")");

                _sourceComboBox.BeginUpdate();
                foreach (PHPVersion version in versions)
                {
                    if (!String.Equals(version.HandlerName, _activeHandlerName, StringComparison.OrdinalIgnoreCase))
                    {
                        version.Version = String.Format(CultureInfo.InvariantCulture, "{0} ({1})", version.Version, version.ScriptProcessor);
                        _sourceComboBox.Items.Add(version);
                    }
                }
                _sourceComboBox.DisplayMember = "Version";
                _sourceComboBox.EndUpdate();

                if (_sourceComboBox.Items.Count > 0)
                {
                    _sourceComboBox.SelectedIndex = 0;
                }
                else
                {
                    _sourceComboBox.Enabled = false;
                    _messageLabel.Text = Resources.MigrateIniDialogNoOtherVersions;
                }
            }
            catch (Exception ex)
            {
                DisplayErrorMessage(ex, Resources.ResourceManager);
            }

            UpdateTaskForm();
        }

        private void OnItemsListViewItemCheck(object sender, ItemCheckEventArgs e)
        {
            // Directives unknown to the active PHP version and extensions without a compatible DLL cannot be copied
            PHPIniMigrationItem item = _itemsListView.Items[e.Index].Tag as PHPIniMigrationItem;
            if (item != null && !PHPIniMigrationStatus.CanBeCopied(item.Status) && e.NewValue == CheckState.Checked)
            {
                e.NewValue = CheckState.Unchecked;
            }
        }

        private void OnItemsListViewItemChecked(object sender, ItemCheckedEventArgs e)
        {
            UpdateTaskForm();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            SetBusy(true);
            StartAsyncTask(OnGetVersionsDoWork, OnGetVersionsCompleted);
        }

        private void OnSourceComboBoxSelectedIndexChanged(object sender, EventArgs e)
        {
            PHPVersion source = _sourceComboBox.SelectedItem as PHPVersion;
            if (source == null)
            {
                return;
            }

            SetBusy(true);
            StartAsyncTask(OnGetPreviewDoWork, OnGetPreviewCompleted, null, source.HandlerName);
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            _sourceComboBox.Enabled = !busy && _sourceComboBox.Items.Count > 0;
            _itemsListView.Enabled = !busy;
            UpdateTaskForm();
        }
    }
}
