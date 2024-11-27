using System;
using System.Linq;
using Dalamud.Game;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Hooking;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace Dalamud.LoadingImage
{
    // ReSharper disable once UnusedType.Global
    public unsafe class LoadingImagePlugin : IDalamudPlugin
    {
        private IDalamudPluginInterface _pi;
        private IFramework _framework;
        private IGameGui _gameGui;
        private IPluginLog _pluginLog;
        private readonly IAddonLifecycle _addonLifecycle;

        private delegate byte HandleTerriChangeDelegate(IntPtr a1, uint a2, byte a3, byte a4, IntPtr a5);

        private Hook<HandleTerriChangeDelegate> handleTerriChangeHook;

        private ExcelSheet<TerritoryType> terris;
        private ExcelSheet<LoadingImage> loadings;
        private ExcelSheet<ContentFinderCondition> cfcs;

        private bool hasLoading = false;

        private int height = 1080;
        private int width = 1920;
        private float scaleX = 0.595f;
        private float scaleY = 0.595f;
        private float X = -60f;
        private float Y = -220f;

        public LoadingImagePlugin(
            IDalamudPluginInterface pluginInterface,
            IDataManager dataManager,
            IGameGui gameGui,
            ISigScanner sigScanner,
            IFramework framework,
            IGameInteropProvider gameInteropProvider,
            IPluginLog pluginLog,
            IAddonLifecycle addonLifecycle)
        {
            _pi = pluginInterface;
            _gameGui = gameGui;
            _framework = framework;
            _pluginLog = pluginLog;
            _addonLifecycle = addonLifecycle;

            this._addonLifecycle.RegisterListener(AddonEvent.PreDraw, "_LocationTitle", this.LocationTitleOnDraw);

            this.terris = dataManager.GetExcelSheet<TerritoryType>();
            this.loadings = dataManager.GetExcelSheet<LoadingImage>();
            this.cfcs = dataManager.GetExcelSheet<ContentFinderCondition>();

            this.handleTerriChangeHook = gameInteropProvider.HookFromAddress<HandleTerriChangeDelegate>(
                sigScanner.ScanText("40 55 53 56 57 41 56 48 8D 6C 24 ?? 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 45 1F 4C 8B F1"),
                this.HandleTerriChangeDetour);

            this.handleTerriChangeHook.Enable();

            #if DEBUG
            this._pi.UiBuilder.Draw += UiBuilderOnOnBuildUi;
            #endif

            framework.Update += FrameworkOnOnUpdateEvent;
        }

        private void LocationTitleOnDraw(AddonEvent type, AddonArgs args)
        {
            var addon = (AtkUnitBase*)args.Addon;
            var regionImageNode = (AtkImageNode*)addon->GetNodeById(3);

            try
            {
                if (this.cfcs.Any(x => x.ContentLinkType == 1 && x.TerritoryType.RowId == this.toLoadingTerri))
                {
                    this._pluginLog.Information("Is InstanceContent zone!");
                    this.hasLoading = false;
                    return;
                }

                if (!this.terris.TryGetRow((uint)this.toLoadingTerri, out var terriZone))
                {
                    this._pluginLog.Information($"terriZone null!");
                    this.hasLoading = false;
                    return;
                }


                if (!this.loadings.TryGetRow(terriZone.LoadingImage.RowId, out var loadingImage))
                {
                    this._pluginLog.Information($"LoadingImage null!");
                    this.hasLoading = false;
                    return;
                }

                if (regionImageNode == null)
                {
                    this._pluginLog.Information("regionImageNode null!");
                    return;
                }

                var asset = regionImageNode->PartsList->Parts[regionImageNode->PartId].UldAsset;
                if (regionImageNode->Type == NodeType.Image && asset != null)
                {
                    var resource = asset->AtkTexture.Resource;
                    if (resource == null)
                    {
                        return;
                    }

                    var name = resource->TexFileResourceHandle->ResourceHandle.FileName;
                    if (name.BufferPtr == null)
                    {
                        return;
                    }

                    var texName = name.ToString();

                    if (!texName.Contains("loadingimage"))
                    {
                        regionImageNode->LoadTexture($"ui/loadingimage/{loadingImage.Name}_hr1.tex");
                        this._pluginLog.Information($"Replacing icon for territory {terriZone.RowId}");
                    }
                }

                this.hasLoading = true;
            }
            catch (Exception e)
            {
                this._pluginLog.Error(e, "Could not replace loading image.");
            }
        }

        private void UiBuilderOnOnBuildUi()
        {
            if (ImGui.Begin("Location test"))
            {
                ImGui.InputInt("W", ref this.width);
                ImGui.InputInt("H", ref this.height);
                ImGui.InputFloat("SX", ref this.scaleX);
                ImGui.InputFloat("SY", ref this.scaleY);
                ImGui.InputFloat("X", ref this.X);
                ImGui.InputFloat("Y", ref this.Y);
                ImGui.Checkbox("hasLoading", ref this.hasLoading);

                ImGui.End();
            }
        }

        private void FrameworkOnOnUpdateEvent(IFramework framework)
        {
            if (this.hasLoading != true)
                return;

            var unitBase = (AtkUnitBase*) _gameGui.GetAddonByName("_LocationTitle", 1);
            var unitBaseShort = (AtkUnitBase*) _gameGui.GetAddonByName("_LocationTitleShort", 1);
            
            this._pluginLog.Info($"unitbase: {(long)unitBase:X} visible: {unitBase->IsVisible}");
            this._pluginLog.Info($"unishort: {(long)unitBaseShort:X} visible: {unitBaseShort->IsVisible}");

            if (unitBase != null && unitBaseShort != null)
            {
                var loadingImage = unitBase->UldManager.NodeList[4];
                var imgNode = (AtkImageNode*) loadingImage;

                if (loadingImage == null)
                    return;

                var asset = imgNode->PartsList->Parts[imgNode->PartId].UldAsset;

                if (loadingImage->Type == NodeType.Image && imgNode != null && asset != null)
                {
                    var resource = asset->AtkTexture.Resource;
                    if (resource == null)
                        return;

                    var name = resource->TexFileResourceHandle->ResourceHandle.FileName;

                    if (name.BufferPtr == null)
                        return;

                    var texName = name.ToString();

                    if (!texName.Contains("loadingimage"))
                    {
                        var t = unitBase->UldManager.NodeList[4];
                        unitBase->UldManager.NodeList[4] = unitBase->UldManager.NodeList[5];
                        unitBase->UldManager.NodeList[5] = t;

                        t->DrawFlags |= 0x1;

                        loadingImage = unitBase->UldManager.NodeList[4];

                        this._pluginLog.Information("Swapped!");
                    }
                }

                loadingImage->Width = (ushort) this.width;
                loadingImage->Height = (ushort) this.height;
                loadingImage->ScaleX = this.scaleX;
                loadingImage->ScaleY = this.scaleY;
                loadingImage->X = this.X;
                loadingImage->Y = this.Y;
                loadingImage->Priority = 0;

                loadingImage->DrawFlags |= 0x1;

                this.hasLoading = false;
            }
        }

        public string Name => "Fancy Loading Screens";

        private int toLoadingTerri = -1;

        private byte HandleTerriChangeDetour(IntPtr a1, uint a2, byte a3, byte a4, IntPtr a5)
        {
            this.toLoadingTerri = (int) a2;
            this._pluginLog.Information($"toLoadingTerri: {this.toLoadingTerri}");
            return this.handleTerriChangeHook.Original(a1, a2, a3, a4, a5);
        }

        public void Dispose()
        {
            this.handleTerriChangeHook.Dispose();
            _framework.Update -= FrameworkOnOnUpdateEvent;

            #if DEBUG
            this._pi.UiBuilder.Draw -= UiBuilderOnOnBuildUi;
            #endif
        }
    }
}
