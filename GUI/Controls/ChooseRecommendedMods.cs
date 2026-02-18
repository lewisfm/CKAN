using System;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
#if NET5_0_OR_GREATER
using System.Runtime.Versioning;
#endif

using CKAN.Configuration;
using CKAN.Versioning;
using CKAN.Games;
using CKAN.GUI.Attributes;

namespace CKAN.GUI
{
    #if NET5_0_OR_GREATER
    [SupportedOSPlatform("windows")]
    #endif
    public partial class ChooseRecommendedMods : UserControl
    {
        public ChooseRecommendedMods()
        {
            InitializeComponent();
        }

        [ForbidGUICalls]
        public void LoadRecommendations(IRegistryQuerier                registry,
                                        IReadOnlyCollection<ReleaseDto> toInstall,
                                        IReadOnlyCollection<ReleaseDto> toUninstall,
                                        GameVersionCriteria             versionCrit,
                                        NetModuleCache                  cache,
                                        IGame                           game,
                                        List<ModuleLabel>               labels,
                                        IConfiguration                  coreConfig,
                                        GUIConfiguration                guiConfig,
                                        Dictionary<ReleaseDto, Tuple<bool, List<string>>> recommendations,
                                        Dictionary<ReleaseDto, List<string>>              suggestions,
                                        Dictionary<ReleaseDto, HashSet<string>>           supporters)
        {
            this.registry    = registry;
            this.toInstall   = toInstall;
            this.toUninstall = toUninstall;
            this.versionCrit = versionCrit;
            this.guiConfig   = guiConfig;
            this.game        = game;
            Util.Invoke(this, () =>
            {
                AlwaysUncheckAllButton.Checked = guiConfig?.SuppressRecommendations ?? false;
                RecommendedModsListView.BeginUpdate();
                RecommendedModsListView.ItemChecked -= RecommendedModsListView_ItemChecked;
                RecommendedModsListView.Items.AddRange(
                    getRecSugRows(cache, game,
                                  labels.Where(mlbl => mlbl.HoldVersion || mlbl.Hide)
                                        .ToArray(),
                                  recommendations, suggestions, supporters,
                                  coreConfig).ToArray());
                MarkConflicts();
                EnableDisableButtons();
                RecommendedModsListView.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);
                RecommendedModsListView.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);
                RecommendedModsListView_ColumnWidthChanged(null, null);
                RecommendedModsListView.EndUpdate();
                // Don't set this before AddRange, it will fire for every row we add!
                RecommendedModsListView.ItemChecked += RecommendedModsListView_ItemChecked;
            });
        }

        [ForbidGUICalls]
        public HashSet<ReleaseDto>? Wait()
        {
            if (Platform.IsMono)
            {
                // Workaround: make sure the ListView headers are drawn
                Util.Invoke(this, RecommendedModsListView.EndUpdate);
            }
            task = new TaskCompletionSource<HashSet<ReleaseDto>?>();
            return task.Task.Result;
        }

        public ListView.SelectedListViewItemCollection SelectedItems
            => RecommendedModsListView.SelectedItems;

        public event Action<ListView.SelectedListViewItemCollection>? OnSelectedItemsChanged;

        public event Action<string>? OnConflictFound;

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            RecommendedModsListView_ColumnWidthChanged(null, null);
        }

        private void RecommendedModsListView_ColumnWidthChanged(object? sender, ColumnWidthChangedEventArgs? args)
        {
            if (args?.ColumnIndex != DescriptionHeader.Index)
            {
                try
                {
                    DescriptionHeader.Width =
                        RecommendedModsListView.Width
                        - ModNameHeader.Width
                        - SourceModulesHeader.Width
                        - SystemInformation.VerticalScrollBarWidth
                        - (2 * SystemInformation.BorderSize.Width);
                }
                catch
                {
                    // Don't freak out if we get a negative width
                }
            }
        }

        private void RecommendedModsListView_SelectedIndexChanged(object? sender, EventArgs? e)
        {
            OnSelectedItemsChanged?.Invoke(RecommendedModsListView.SelectedItems);
        }

        private void RecommendedModsListView_ItemChecked(object? sender, ItemCheckedEventArgs? e)
        {
            var module = e?.Item.Tag as ReleaseDto;
            if (module?.IsDLC ?? false)
            {
                if (e != null && e.Item.Checked)
                {
                    e.Item.Checked = false;
                }
            }
            else
            {
                MarkConflicts();
                EnableDisableButtons();
            }
        }

        private void MarkConflicts()
        {
            if (registry != null && versionCrit != null && game != null
                && Main.Instance?.CurrentInstance is GameInstance inst)
            {
                try
                {
                    var resolver = new RelationshipResolver(
                        RecommendedModsListView.CheckedItems
                                               .OfType<ListViewItem>()
                                               .Select(item => item.Tag as ReleaseDto)
                                               .OfType<ReleaseDto>()
                                               .Concat(toInstall)
                                               .Distinct(),
                        toUninstall,
                        RelationshipResolverOptions.ConflictsOpts(inst.StabilityToleranceConfig),
                        registry, game, versionCrit);
                    var conflicts = resolver.ConflictList;
                    foreach (var item in RecommendedModsListView.Items.Cast<ListViewItem>()
                        // Apparently ListView handes AddRange by:
                        //   1. Expanding the Items list to the new size by filling it with nulls
                        //   2. One by one, replace each null with a real item and call _ItemChecked
                        // ... so the Items list can contain null!!
                        .OfType<ListViewItem>())
                    {
                        item.BackColor = item.Tag is ReleaseDto m && conflicts.ContainsKey(m)
                            ? Color.LightCoral
                            : Color.Empty;
                    }
                    RecommendedModsContinueButton.Enabled = conflicts.Count == 0;
                    OnConflictFound?.Invoke(string.Join("; ", resolver.ConflictDescriptions));
                }
                catch (DependenciesNotSatisfiedKraken k)
                {
                    var rows = RecommendedModsListView.Items
                                                      .OfType<ListViewItem>()
                                                      .Where(item => item.Tag is ReleaseDto mod
                                                                     && k.unsatisfied.Any(stack =>
                                                                         stack.Any(rr => rr.Contains(mod))));
                    foreach (var row in rows)
                    {
                        row.BackColor = Color.LightCoral;
                    }
                    RecommendedModsContinueButton.Enabled = false;
                    OnConflictFound?.Invoke(k.Message);
                }
            }
        }

        private IEnumerable<ListViewItem> getRecSugRows(
            NetModuleCache                                    cache,
            IGame                                             game,
            ModuleLabel[]                                     uncheckLabels,
            Dictionary<ReleaseDto, Tuple<bool, List<string>>> recommendations,
            Dictionary<ReleaseDto, List<string>>              suggestions,
            Dictionary<ReleaseDto, HashSet<string>>           supporters,
            IConfiguration                                    coreConfig)
            => recommendations.Select(kvp => getRecSugItem(cache,
                                                           kvp.Key,
                                                           string.Join(", ", kvp.Value.Item2),
                                                           RecommendationsGroup,
                                                           (!guiConfig?.SuppressRecommendations ?? true)
                                                               && kvp.Value.Item1
                                                               && !uncheckLabels.Any(mlbl =>
                                                                   mlbl.ContainsModule(game, kvp.Key.identifier)),
                                                           coreConfig))
                              .OrderBy(SecondColumn)
                              .Concat(suggestions.Select(kvp => getRecSugItem(cache,
                                                                              kvp.Key,
                                                                              string.Join(", ", kvp.Value),
                                                                              SuggestionsGroup,
                                                                              false,
                                                                              coreConfig))
                                                 .OrderBy(SecondColumn))
                              .Concat(supporters.Select(kvp => getRecSugItem(cache,
                                                                             kvp.Key,
                                                                             string.Join(", ", kvp.Value.Order()),
                                                                             SupportedByGroup,
                                                                             false,
                                                                             coreConfig))
                                                .OrderBy(SecondColumn));

        private string SecondColumn(ListViewItem item)
            => //item.SubItems is [_, {Text: string text}, ..]
               item.SubItems.Count > 1
               && item.SubItems[0].Text is string text
                   ? text
                   : "";

        private static ListViewItem getRecSugItem(NetModuleCache cache,
                                                  ReleaseDto     module,
                                                  string         descrip,
                                                  ListViewGroup  group,
                                                  bool           check,
                                                  IConfiguration config)
        => new ListViewItem(new string[]
            {
                module.IsDLC ? module.name : cache.DescribeAvailability(config, module),
                descrip,
                module.@abstract
            })
            {
                Tag     = module,
                Checked = check && !module.IsDLC,
                Group   = group
            };

        private void UncheckAllButton_Click(object? sender, EventArgs? e)
        {
            CheckUncheckRows(RecommendedModsListView.Items, false);
        }

        private void AlwaysUncheckAllButton_CheckedChanged(object? sender, EventArgs? e)
        {
            if (guiConfig != null && guiConfig.SuppressRecommendations != AlwaysUncheckAllButton.Checked
                && Main.Instance?.CurrentInstance is GameInstance inst)
            {
                guiConfig.SuppressRecommendations = AlwaysUncheckAllButton.Checked;
                guiConfig.Save(inst);
                if (guiConfig.SuppressRecommendations)
                {
                    UncheckAllButton_Click(null, null);
                }
            }
        }

        private void CheckAllButton_Click(object? sender, EventArgs? e)
        {
            CheckUncheckRows(RecommendedModsListView.Items, true);
        }

        private void CheckRecommendationsButton_Click(object? sender, EventArgs? e)
        {
            CheckUncheckRows(RecommendationsGroup.Items, true);
        }

        private void CheckSuggestionsButton_Click(object? sender, EventArgs? e)
        {
            CheckUncheckRows(SuggestionsGroup.Items, true);
        }

        private void CheckUncheckRows(ListView.ListViewItemCollection items,
                                      bool check)
        {
            RecommendedModsListView.BeginUpdate();
            RecommendedModsListView.ItemChecked -= RecommendedModsListView_ItemChecked;
            foreach (ListViewItem item in items)
            {
                if (item.Checked != check && NotDLC(item))
                {
                    item.Checked = check;
                }
            }
            MarkConflicts();
            EnableDisableButtons();
            RecommendedModsListView.EndUpdate();
            RecommendedModsListView.ItemChecked += RecommendedModsListView_ItemChecked;
        }

        private void EnableDisableButtons()
        {
            CheckAllButton.Enabled = RecommendedModsListView.Items
                                                            .OfType<ListViewItem>()
                                                            .Where(NotDLC)
                                                            .Any(lvi => !lvi.Checked);
            CheckRecommendationsButton.Enabled = RecommendationsGroup.Items
                                                                     .OfType<ListViewItem>()
                                                                     .Where(NotDLC)
                                                                     .Any(lvi => !lvi.Checked);
        }

        private bool NotDLC(ListViewItem item)
            => item.Tag is ReleaseDto mod && !mod.IsDLC;

        private void RecommendedModsCancelButton_Click(object? sender, EventArgs? e)
        {
            task?.SetResult(null);
            RecommendedModsListView.Items.Clear();
            RecommendedModsListView.ItemChecked -= RecommendedModsListView_ItemChecked;
        }

        private void RecommendedModsContinueButton_Click(object? sender, EventArgs? e)
        {
            task?.SetResult(RecommendedModsListView.CheckedItems
                                                   .OfType<ListViewItem>()
                                                   .Select(item => item.Tag as ReleaseDto)
                                                   .OfType<ReleaseDto>()
                                                   .ToHashSet());
            RecommendedModsListView.Items.Clear();
            RecommendedModsListView.ItemChecked -= RecommendedModsListView_ItemChecked;
        }

        private IRegistryQuerier?               registry;
        private IReadOnlyCollection<ReleaseDto> toInstall   = Array.Empty<ReleaseDto>();
        private IReadOnlyCollection<ReleaseDto> toUninstall = Array.Empty<ReleaseDto>();
        private GameVersionCriteria?            versionCrit;
        private GUIConfiguration?               guiConfig;
        private IGame?                          game;
        private TaskCompletionSource<HashSet<ReleaseDto>?>? task;
    }
}
