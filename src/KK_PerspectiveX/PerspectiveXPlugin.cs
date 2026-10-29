using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using Studio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityStandardAssets.ImageEffects;

namespace PerspectiveX
{
    public enum VrFollowButton
    {
        [Description("None")]
        None = 0,
        [Description("Trigger")]
        Trigger,
        [Description("Grip")]
        Grip,
        [Description("Touchpad / Thumbstick press")]
        Touchpad,
        [Description("Application menu")]
        ApplicationMenu,
        [Description("A (right hand) / X (left hand)")]
        A,
        [Description("B (right hand) / Y (left hand)")]
        B,
    }

    [BepInPlugin("bucky.kk.perspectivex", "PerspectiveX", "1.3.4")]
    [BepInIncompatibility("keelhauled.realpov")]
    public class PerspectiveXPlugin : BaseUnityPlugin
    {
        private ConfigEntry<KeyboardShortcut> ToggleKey { get; set; }
        private ConfigEntry<KeyboardShortcut> CyclePrevKey { get; set; }
        private ConfigEntry<KeyboardShortcut> CycleNextKey { get; set; }
        private ConfigEntry<KeyboardShortcut> FpsModeKey { get; set; }
        private ConfigEntry<KeyboardShortcut> RollLeftKey { get; set; }
        private ConfigEntry<KeyboardShortcut> RollRightKey { get; set; }
        private ConfigEntry<KeyboardShortcut> RollResetKey { get; set; }
        private ConfigEntry<KeyboardShortcut> CamLockKey { get; set; }
        private ConfigEntry<KeyboardShortcut> PanelKey { get; set; }
        private ConfigEntry<KeyboardShortcut> VrFollowCycleKey { get; set; }
        private ConfigEntry<VrFollowButton> VrFollowCycleButton { get; set; }
        private ConfigEntry<VrMappedKey> VrMapTrigger { get; set; }
        private ConfigEntry<VrMappedKey> VrMapGrip { get; set; }
        private ConfigEntry<VrMappedKey> VrMapUp { get; set; }
        private ConfigEntry<VrMappedKey> VrMapDown { get; set; }
        private ConfigEntry<VrMappedKey> VrMapLeft { get; set; }
        private ConfigEntry<VrMappedKey> VrMapRight { get; set; }
        private ConfigEntry<VrMappedKey> VrMapCenter { get; set; }
        private ConfigEntry<float> DefaultFov { get; set; }
        private ConfigEntry<bool> ScrollFovEnabled { get; set; }
        private ConfigEntry<string> ViewPresets { get; set; }
        private ConfigEntry<float> MouseSensitivity { get; set; }
        private ConfigEntry<float> PositionSmoothing { get; set; }
        private ConfigEntry<float> HeadSway { get; set; }
        private ConfigEntry<float> ForwardOffset { get; set; }
        private ConfigEntry<float> UpOffset { get; set; }
        private ConfigEntry<float> PitchLimit { get; set; }
        private ConfigEntry<float> NearClip { get; set; }
        private ConfigEntry<bool> HideHead { get; set; }
        private ConfigEntry<bool> AlignWithBody { get; set; }
        private ConfigEntry<bool> EnableInFreeRoam { get; set; }
        private ConfigEntry<bool> VrSupportMain { get; set; }
        private ConfigEntry<bool> VrSupportStudio { get; set; }
        private ConfigEntry<bool> VrHeadEntry { get; set; }
        private ConfigEntry<float> VrForwardOffset { get; set; }
        private ConfigEntry<float> VrUpOffset { get; set; }
        private ConfigEntry<float> VrNearClip { get; set; }
        private ConfigEntry<bool> HideHeadVrMain { get; set; }
        private ConfigEntry<bool> HideHeadVrStudio { get; set; }

        // ===== 虚拟按键状态（用于让手柄映射的键能被插件自己的快捷键识别） =====
        private readonly HashSet<KeyCode> virtualHeldKeys = new HashSet<KeyCode>();
        private readonly HashSet<KeyCode> virtualDownKeys = new HashSet<KeyCode>();

        private void Awake()
        {
            this.ToggleKey = base.Config.Bind<KeyboardShortcut>("Keyboard shortcuts", "Toggle POV", new KeyboardShortcut(KeyCode.Backspace, new KeyCode[0]), "");
            this.CyclePrevKey = base.Config.Bind<KeyboardShortcut>("Keyboard shortcuts", "Previous POV character", new KeyboardShortcut(KeyCode.LeftArrow, new KeyCode[] { KeyCode.LeftControl, KeyCode.LeftShift }), "Switch the POV to the previous character while POV is active.");
            this.CycleNextKey = base.Config.Bind<KeyboardShortcut>("Keyboard shortcuts", "Next POV character", new KeyboardShortcut(KeyCode.RightArrow, new KeyCode[] { KeyCode.LeftControl, KeyCode.LeftShift }), "Switch the POV to the next character while POV is active.");
            this.FpsModeKey = base.Config.Bind<KeyboardShortcut>("Keyboard shortcuts", "Toggle FPS mouse look", new KeyboardShortcut(KeyCode.L, new KeyCode[] { KeyCode.LeftControl }), "Optional hands-free mode: locks and hides the cursor so the mouse looks around directly, like an FPS. Press again to get the cursor back. The normal mode is dragging with the left mouse button.");
            this.RollLeftKey = base.Config.Bind<KeyboardShortcut>("Keyboard shortcuts", "Roll camera left", new KeyboardShortcut(KeyCode.Comma, new KeyCode[0]), "Hold to tilt the camera counterclockwise.");
            this.RollRightKey = base.Config.Bind<KeyboardShortcut>("Keyboard shortcuts", "Roll camera right", new KeyboardShortcut(KeyCode.Period, new KeyCode[0]), "Hold to tilt the camera clockwise.");
            this.RollResetKey = base.Config.Bind<KeyboardShortcut>("Keyboard shortcuts", "Reset camera roll", new KeyboardShortcut(KeyCode.Slash, new KeyCode[0]), "Reset the camera tilt angle. If in VR, the function changes to Reset Rotation Follow and reverts to position-only tracking mode.");
            this.CamLockKey = base.Config.Bind<KeyboardShortcut>("Keyboard shortcuts", "Lock camera in place", new KeyboardShortcut(KeyCode.Semicolon, new KeyCode[0]), "Freeze the camera where it is so it stops following the head, e.g. during caress animations that throw the head around. Looking around still works. Press again to unlock.");
            this.PanelKey = base.Config.Bind<KeyboardShortcut>("Keyboard shortcuts", "Toggle presets panel", new KeyboardShortcut(KeyCode.P, new KeyCode[] { KeyCode.LeftControl }), "Show/hide the in-game panel with view presets and saved views. In Studio the panel can also be opened with the PerspectiveX button in the bottom-left toolbar.");
            this.VrFollowCycleKey = base.Config.Bind<KeyboardShortcut>("Keyboard shortcuts", "VR cycle rotation follow (key)", new KeyboardShortcut(KeyCode.V, new KeyCode[0]), "VR only. Press to cycle: position only -> position + yaw -> position + pitch -> position + yaw + pitch -> position + yaw + pitch + roll -> position only.");
            this.VrFollowCycleButton = base.Config.Bind<VrFollowButton>("Keyboard shortcuts", "VR cycle rotation follow (VR button)", VrFollowButton.None, "VR only. Optional. Press the assigned VR controller button to cycle the same as the key above. Works on either hand.");
            this.VrMapTrigger = base.Config.Bind<VrMappedKey>("VR controller mapping", "Trigger", VrMappedKey.None, new ConfigDescription("Map the VR Trigger button to a keyboard/mouse action. Applied on both hands.", null, new object[] { new ConfigurationManagerAttributes { Order = new int?(-1) } }));
            this.VrMapGrip = base.Config.Bind<VrMappedKey>("VR controller mapping", "Grip", VrMappedKey.None, new ConfigDescription("Map the VR Grip button to a keyboard/mouse action. Applied on both hands.", null, new object[] { new ConfigurationManagerAttributes { Order = new int?(-2) } }));
            this.VrMapUp = base.Config.Bind<VrMappedKey>("VR controller mapping", "Up", VrMappedKey.None, new ConfigDescription("Map Touchpad/Thumbstick Up to a keyboard/mouse action. Press and hold the touchpad in that direction.", null, new object[] { new ConfigurationManagerAttributes { Order = new int?(-3) } }));
            this.VrMapDown = base.Config.Bind<VrMappedKey>("VR controller mapping", "Down", VrMappedKey.V, new ConfigDescription("Map Touchpad/Thumbstick Down to a keyboard/mouse action.", null, new object[] { new ConfigurationManagerAttributes { Order = new int?(-4) } }));
            this.VrMapLeft = base.Config.Bind<VrMappedKey>("VR controller mapping", "Left", VrMappedKey.None, new ConfigDescription("Map Touchpad/Thumbstick Left to a keyboard/mouse action.", null, new object[] { new ConfigurationManagerAttributes { Order = new int?(-5) } }));
            this.VrMapRight = base.Config.Bind<VrMappedKey>("VR controller mapping", "Right", VrMappedKey.None, new ConfigDescription("Map Touchpad/Thumbstick Right to a keyboard/mouse action.", null, new object[] { new ConfigurationManagerAttributes { Order = new int?(-6) } }));
            this.VrMapCenter = base.Config.Bind<VrMappedKey>("VR controller mapping", "Center", VrMappedKey.None, new ConfigDescription("Map Touchpad/Thumbstick Center press to a keyboard/mouse action.", null, new object[] { new ConfigurationManagerAttributes { Order = new int?(-7) } }));

            this.saveSlotKeys = new ConfigEntry<KeyboardShortcut>[3];
            this.loadSlotKeys = new ConfigEntry<KeyboardShortcut>[3];
            this.viewSlots = new ConfigEntry<string>[3];
            for (int i = 0; i < 3; i++)
            {
                KeyCode keyCode = KeyCode.Alpha1 + i;
                this.saveSlotKeys[i] = base.Config.Bind<KeyboardShortcut>("Keyboard shortcuts", string.Format("Save view slot {0}", i + 1), new KeyboardShortcut(keyCode, new KeyCode[] { KeyCode.LeftControl, KeyCode.LeftShift }), "Save the current POV view (FOV, look direction, tilt, camera offsets) into this slot. Slots are kept between game sessions.");
                this.loadSlotKeys[i] = base.Config.Bind<KeyboardShortcut>("Keyboard shortcuts", string.Format("Load view slot {0}", i + 1), new KeyboardShortcut(keyCode, new KeyCode[] { KeyCode.LeftControl }), "Recall the view saved in this slot.");
                this.viewSlots[i] = base.Config.Bind<string>("Saved views", string.Format("Slot {0}", i + 1), "", new ConfigDescription("Saved automatically by the save-view-slot shortcuts, no need to edit by hand.", null, new object[] { new ConfigurationManagerAttributes { IsAdvanced = new bool?(true) } }));
            }

            this.DefaultFov = base.Config.Bind<float>("General", "Field of view", 60f, new ConfigDescription("Vertical FOV. Can also be adjusted with the scroll wheel while in POV.", new AcceptableValueRange<float>(20f, 120f), new object[0]));
            this.ScrollFovEnabled = base.Config.Bind<bool>("General", "Scroll wheel adjusts FOV", true, "Turn off if you keep changing the FOV by accident while scrolling in POV.");
            this.ViewPresets = base.Config.Bind<string>("General", "View presets", "", new ConfigDescription("One-click setups for FOV, position smoothing and forward offset. Pick one, then fine-tune the sliders if you feel like it.", null, new object[] { new ConfigurationManagerAttributes { CustomDrawer = new Action<ConfigEntryBase>(this.DrawViewPresets), HideDefaultButton = new bool?(true) } }));
            this.customPresets = new ConfigEntry<string>[5];
            for (int j = 0; j < 5; j++)
            {
                this.customPresets[j] = base.Config.Bind<string>("Custom presets", string.Format("Custom preset {0}", j + 1), "", new ConfigDescription("Managed from the in-game presets panel, no need to edit by hand.", null, new object[] { new ConfigurationManagerAttributes { IsAdvanced = new bool?(true) } }));
            }
            this.TryAddStudioToolbarButton();
            this.MouseSensitivity = base.Config.Bind<float>("General", "Mouse sensitivity", 1f, new ConfigDescription("", new AcceptableValueRange<float>(0.1f, 5f), new object[0]));
            this.PositionSmoothing = base.Config.Bind<float>("Comfort", "Position smoothing", 0.65f, new ConfigDescription("How much head motion from animations is filtered out of the camera position. 0 = camera rigidly locked to the head, 1 = very smooth and floaty.", new AcceptableValueRange<float>(0f, 1f), new object[0]));
            this.HeadSway = base.Config.Bind<float>("Comfort", "Animation sway", 0.15f, new ConfigDescription("How much of the head animation's rotation leaks into your view. 0 = perfectly stable FPS camera, 1 = fully follow the animation (dizzy!). Roll/tilt is always removed.", new AcceptableValueRange<float>(0f, 1f), new object[0]));
            this.PitchLimit = base.Config.Bind<float>("Comfort", "Pitch limit", 85f, new ConfigDescription("How far up/down you can look, in degrees. 90 is straight up/down. Above 90 you can keep going past vertical, e.g. to look back over the head while lying down; the view turns upside down there.", new AcceptableValueRange<float>(30f, 180f), new object[0]));
            this.ForwardOffset = base.Config.Bind<float>("Camera", "Forward offset", 0f, new ConfigDescription("Camera distance in front of the eyes, in meters. Raise it if you can see the inside of the head.", new AcceptableValueRange<float>(0f, 0.2f), new object[0]));
            this.UpOffset = base.Config.Bind<float>("Camera", "Up offset", 0f, new ConfigDescription("Vertical camera offset from the eye line, in meters.", new AcceptableValueRange<float>(-0.1f, 0.1f), new object[0]));
            this.NearClip = base.Config.Bind<float>("Camera", "Near clip plane", 0.002f, new ConfigDescription("Lower values let very close geometry render without being cut off.", new AcceptableValueRange<float>(0.001f, 0.1f), new object[0]));
            this.HideHead = base.Config.Bind<bool>("General", "Hide character head", true, "Hide the POV character's head (incl. hair and head accessories) so nothing clips into the view. Flat-screen only.");
            this.EnableInFreeRoam = base.Config.Bind<bool>("General", "Use POV during free-roam", false, "OFF by default. When you walk around the map the game already has its own first-person view (press Space) that this mod clashes with - with both on, the built-in view hides the body while this one hides the head, so the character can vanish and the camera gets jittery. While this is off, Backspace does nothing during free-roam (it still works in H-scenes and Studio). Turn it on to use PerspectiveX here anyway: it takes over and suppresses the built-in Space view, but it's less reliable than in H/Studio - if the character disappears or the view glitches, turn this back off and use the game's own Space camera instead.");
            this.AlignWithBody = base.Config.Bind<bool>("Comfort", "Align camera with body", false, "Tilt the camera to follow the character's body orientation, so the view isn't kept artificially level when they're lying on their side or leaning. Fine-tune with the roll keys.");
            this.BindVrSettings();

            this.DefaultFov.SettingChanged += delegate(object s, EventArgs e) { this.fov = this.DefaultFov.Value; };
            this.HideHead.SettingChanged += delegate(object s, EventArgs e) { if (this.povEnabled && this.chara && !this.vrPov) { this.ApplyHeadHiding(); } };
            this.HideHeadVrMain.SettingChanged += delegate(object s, EventArgs e) { if (this.povEnabled && this.chara && this.vrPov && !this.isStudio) { this.ApplyHeadHiding(); } };
            this.HideHeadVrStudio.SettingChanged += delegate(object s, EventArgs e) { if (this.povEnabled && this.chara && this.vrPov && this.isStudio) { this.ApplyHeadHiding(); } };
            this.EnableInFreeRoam.SettingChanged += delegate(object s, EventArgs e) { if (this.EnableInFreeRoam.Value) { base.Logger.LogMessage("Heads up: free-roam POV fights the game's own Space view and can make your character invisible or the camera glitchy. If it misbehaves, turn it back off and use Space instead."); } };
            SceneManager.sceneLoaded += delegate(Scene scene, LoadSceneMode mode) { this.hFlag = global::UnityEngine.Object.FindObjectOfType<HFlag>(); this.charaList = null; };
            SceneManager.sceneUnloaded += delegate(Scene scene) { this.charaList = null; };
            Camera.onPreCull = (Camera.CameraCallback)Delegate.Combine(Camera.onPreCull, new Camera.CameraCallback(this.OnCameraPreCull));
        }

        private void OnDestroy()
        {
            Camera.onPreCull = (Camera.CameraCallback)Delegate.Remove(Camera.onPreCull, new Camera.CameraCallback(this.OnCameraPreCull));
            this.StopVrDetection();
            if (this.povEnabled) { this.DisablePov(); }
            this.SetCursorLock(false);
            this.UnhookCameraConditions();
        }

        private void Update()
        {
            // 顺序很重要：先做手柄映射登记虚拟按键 → 再消费热键 → 帧末清空本帧按下标记
            this.UpdateVrButtonMappings();
            this.UpdateHotkeys();
            this.UpdatePanelInputSuppression();
            this.virtualDownKeys.Clear();
        }

        private bool IsVrEnabled()
        {
            return this.isStudio ? this.VrSupportStudio.Value : this.VrSupportMain.Value;
        }

        // ===== 虚拟按键辅助 =====

        private static bool IsModifierKey(KeyCode k)
        {
            return k == KeyCode.LeftShift || k == KeyCode.RightShift
                || k == KeyCode.LeftControl || k == KeyCode.RightControl
                || k == KeyCode.LeftAlt || k == KeyCode.RightAlt
                || k == KeyCode.LeftCommand || k == KeyCode.RightCommand;
        }

        private bool IsShortcutDown(KeyboardShortcut ks)
        {
            // 原生优先
            if (ks.IsDown()) { return true; }
            // 虚拟层：主键必须本帧按下
            if (!this.virtualDownKeys.Contains(ks.MainKey)) { return false; }
            // 所有声明的修饰键必须处于按下（原生或虚拟）
            foreach (KeyCode m in ks.Modifiers)
            {
                if (!Input.GetKey(m) && !this.virtualHeldKeys.Contains(m)) { return false; }
            }
            // 不允许额外修饰键也按着，避免 Ctrl+V 被当作裸 V
            foreach (KeyCode k in Enum.GetValues(typeof(KeyCode)))
            {
                if (k == ks.MainKey) { continue; }
                if (ks.Modifiers.Contains(k)) { continue; }
                if (!IsModifierKey(k)) { continue; }
                if (Input.GetKey(k) || this.virtualHeldKeys.Contains(k)) { return false; }
            }
            return true;
        }

        private void UpdateHotkeys()
        {
            bool flag = this.presetNameFocused;
            if (!flag && this.IsShortcutDown(this.PanelKey.Value)) { this.SetPanelVisible(!this.panelVisible); }
            if (this.panelVisible) { this.EnsureCameraConditionHooks(); this.UpdateHiddenHeadScan(); }
            if (this.cursorLocked && !this.dragging && !this.fpsMode) { this.SetCursorLock(false); }
            if (!flag && this.IsShortcutDown(this.ToggleKey.Value))
            {
                if (this.povEnabled) { this.DisablePov(); return; }
                if (this.IsFreeRoam() && !this.EnableInFreeRoam.Value)
                {
                    base.Logger.LogMessage("PerspectiveX POV is off during free-roam - it clashes with the game's own first-person view (Space). Turn on \"Use POV during free-roam\" in the plugin settings to use it here.");
                    return;
                }
                this.EnablePov();
                return;
            }
            else
            {
                if (!this.povEnabled) { this.UpdateVrHeadEntry(); return; }
                if (!this.chara || !this.chara.objTop) { this.DisablePov(); return; }
                if (this.vrPov && !this.CheckVrPovStillValid()) { this.DisablePov(); return; }
                if ((this.vrPov ? (this.isStudio ? this.HideHeadVrStudio.Value : this.HideHeadVrMain.Value) : this.HideHead.Value) && Time.time >= this.nextHeadRescan)
                {
                    this.nextHeadRescan = Time.time + 0.5f;
                    this.HideHeadRenderers();
                }
                if (!flag && this.IsShortcutDown(this.CyclePrevKey.Value)) { this.CycleCharacter(-1); return; }
                if (!flag && this.IsShortcutDown(this.CycleNextKey.Value)) { this.CycleCharacter(1); return; }
                if (this.vrPov)
                {
                    if (!flag && this.IsShortcutDown(this.RollResetKey.Value))
                    {
                        if (this.vrFollowMode != 0)
                        {
                            this.vrFollowMode = 0;
                            this.vrPrevFilteredDelta = Quaternion.identity;
                            base.Logger.LogMessage("VR rotation follow: OFF");
                        }
                    }

                    bool cycleRequested = false;
                    if (!flag && this.IsShortcutDown(this.VrFollowCycleKey.Value)) { cycleRequested = true; }

                    if (!flag && this.VrFollowCycleButton.Value != VrFollowButton.None)
                    {
                        bool rawNow = this.ReadVrFollowButtonRaw();
                        if (rawNow && !this.vrFollowButtonPrev) { cycleRequested = true; }
                        this.vrFollowButtonPrev = rawNow;
                    }
                    else
                    {
                        this.vrFollowButtonPrev = false;
                    }

                    if (cycleRequested) { this.CycleVrFollow(); }
                    this.UpdateCommonPovKeys(flag);
                    return;
                }
                this.vrFollowButtonPrev = false;
                if (!flag && this.IsShortcutDown(this.FpsModeKey.Value)) { this.fpsMode = !this.fpsMode; this.dragging = false; this.SetCursorLock(this.fpsMode); }
                if (!this.fpsMode)
                {
                    if (!this.dragging && Input.GetMouseButtonDown(0) && !PerspectiveXPlugin.IsPointerOverUI() && !this.IsMouseOverPanel()) { this.dragging = true; this.SetCursorLock(true); }
                    if (this.dragging && !Input.GetMouseButton(0)) { this.dragging = false; this.SetCursorLock(false); }
                }
                if (this.fpsMode || this.dragging)
                {
                    float num = Input.GetAxis("Mouse X") * this.MouseSensitivity.Value;
                    float num2 = -Input.GetAxis("Mouse Y") * this.MouseSensitivity.Value;
                    float num6 = (this.manualRoll + (this.AlignWithBody.Value ? this.bodyRoll : 0f)) * 0.017453292f;
                    float num3 = Mathf.Cos(num6);
                    float num4 = Mathf.Sin(num6);
                    float num5 = ((Mathf.Cos(this.pitch * 0.017453292f) < 0f) ? (-1f) : 1f);
                    this.yaw += (num * num3 + num2 * num4) * num5;
                    if (this.yaw > 180f) { this.yaw -= 360f; } else if (this.yaw < -180f) { this.yaw += 360f; }
                    this.pitch = Mathf.Clamp(this.pitch + num2 * num3 - num * num4, -this.PitchLimit.Value, this.PitchLimit.Value);
                }
                if (this.ScrollFovEnabled.Value)
                {
                    float axis = Input.GetAxis("Mouse ScrollWheel");
                    if (axis != 0f && (this.fpsMode || this.dragging || (!PerspectiveXPlugin.IsPointerOverUI() && !this.IsMouseOverPanel()))) { this.fov = Mathf.Clamp(this.fov - axis * 15f, 20f, 120f); }
                }
                if (flag) { return; }
                bool flag2 = this.RollLeftKey.Value.IsPressed();
                bool flag3 = this.RollRightKey.Value.IsPressed();
                if (this.IsShortcutDown(this.RollResetKey.Value)) { this.manualRoll = 0f; }
                else if (flag2 != flag3)
                {
                    this.manualRoll += (flag2 ? 1f : (-1f)) * 60f * Time.deltaTime;
                    if (this.manualRoll > 180f) { this.manualRoll -= 360f; } else if (this.manualRoll < -180f) { this.manualRoll += 360f; }
                }
                this.UpdateCommonPovKeys(flag);
                return;
            }
        }

        private void UpdateCommonPovKeys(bool typingInUi)
        {
            if (typingInUi) { return; }
            if (this.IsShortcutDown(this.CamLockKey.Value))
            {
                this.camLocked = !this.camLocked;
                if (!this.camLocked) { this.swayInit = false; }
                base.Logger.LogMessage(this.camLocked ? "Camera locked in place" : "Camera unlocked");
            }
            for (int i = 0; i < 3; i++)
            {
                if (this.IsShortcutDown(this.saveSlotKeys[i].Value)) { this.SaveViewSlot(i); }
                else if (this.IsShortcutDown(this.loadSlotKeys[i].Value)) { this.LoadViewSlot(i); }
            }
        }

        private static bool IsPointerOverUI() { return GUIUtility.hotControl != 0 || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()); }

        private void UpdatePanelInputSuppression()
        {
            if (!this.panelVisible || this.fpsMode || this.dragging || PerspectiveXPlugin.IsCursorPinned() || !this.IsMouseOverPanel()) { this.inputSuppressFrames = 0; return; }
            Vector3 mousePosition = Input.mousePosition;
            if (this.inputSuppressFrames == 0 || mousePosition != this.lastSuppressMousePos) { this.lastSuppressMousePos = mousePosition; this.inputSuppressFrames = 1; }
            else { int num = this.inputSuppressFrames + 1; this.inputSuppressFrames = num; if (num > 180) { return; } }
            Input.ResetInputAxes();
        }

        private static bool IsCursorPinned() { bool flag; try { flag = GameCursor.isLock; } catch (Exception) { flag = false; } return flag; }

        private void WarnAboutHiddenHeadsOnce()
        {
            if (!this.isStudio || this.hiddenHeadNoticeShown) { return; }
            this.hiddenHeadNoticeShown = true;
            this.hiddenHeadChars = this.ScanHiddenHeads(false);
            if (this.hiddenHeadChars > 0) { base.Logger.LogMessage(this.hiddenHeadChars.ToString() + " character(s) in this scene have their head hidden by the scene file itself - open the PerspectiveX panel (" + this.PanelKey.Value.ToString() + ") to bring it back."); }
        }

        private void UpdateHiddenHeadScan()
        {
            if (!this.isStudio || Time.time < this.nextHiddenHeadScan) { return; }
            this.nextHiddenHeadScan = Time.time + 1f;
            this.hiddenHeadChars = this.ScanHiddenHeads(false);
        }

        private void OnCameraPreCull(Camera renderingCam)
        {
            if (!this.povEnabled || this.cam == null || renderingCam != this.cam) { return; }
            if (!this.chara || !this.chara.objHeadBone) { return; }
            if (this.vrPov && !this.IsFirstVrCullThisFrame()) { return; }
            Transform transform = this.chara.objHeadBone.transform;
            Quaternion rotation = transform.rotation;
            Vector3 vector = (this.vrPov ? new Vector3(0f, this.VrUpOffset.Value, this.VrForwardOffset.Value) : new Vector3(0f, this.UpOffset.Value, this.ForwardOffset.Value));
            Vector3 vector2 = PerspectiveXPlugin.GetEyeMidpoint(this.chara, transform) + rotation * vector;
            float deltaTime = Time.deltaTime;
            float value = this.PositionSmoothing.Value;
            if (!this.smoothPosInit) { this.smoothPos = vector2; this.smoothPosInit = true; }
            else if (!this.camLocked)
            {
                if (value <= 0f) { this.smoothPos = vector2; }
                else { float num = Mathf.Lerp(70f, 4f, value); this.smoothPos = Vector3.Lerp(this.smoothPos, vector2, 1f - Mathf.Exp(-num * deltaTime)); }
            }
            if (this.vrPov) { this.PlaceVrRig(); return; }
            float num2 = this.manualRoll;
            if (this.AlignWithBody.Value)
            {
                Vector3 vector3 = rotation * Vector3.forward;
                if (!this.camLocked && Mathf.Abs(vector3.y) < 0.99f)
                {
                    Vector3 normalized = Vector3.ProjectOnPlane(Vector3.up, vector3).normalized;
                    Vector3 normalized2 = Vector3.ProjectOnPlane(rotation * Vector3.up, vector3).normalized;
                    float num3 = Mathf.Atan2(Vector3.Dot(Vector3.Cross(normalized, normalized2), vector3), Vector3.Dot(normalized, normalized2)) * 57.29578f;
                    if (!this.bodyRollInit) { this.bodyRoll = num3; this.bodyRollInit = true; }
                    else { this.bodyRoll = Mathf.LerpAngle(this.bodyRoll, num3, 1f - Mathf.Exp(-3f * deltaTime)); }
                }
                num2 += this.bodyRoll;
            }
            else { this.bodyRollInit = false; }
            Quaternion quaternion = Quaternion.Euler(this.pitch, this.yaw, num2);
            float value2 = this.HeadSway.Value;
            if (value2 > 0f && !this.camLocked)
            {
                Vector3 vector4 = rotation * Vector3.forward;
                if (Mathf.Abs(vector4.y) < 0.999f)
                {
                    Quaternion quaternion2 = Quaternion.LookRotation(vector4, Vector3.up);
                    if (!this.swayInit) { this.swayBaseline = quaternion2; this.swayInit = true; }
                    this.swayBaseline = Quaternion.Slerp(this.swayBaseline, quaternion2, 1f - Mathf.Exp(-2.5f * deltaTime));
                    Quaternion quaternion3 = quaternion2 * Quaternion.Inverse(this.swayBaseline);
                    quaternion = Quaternion.Slerp(Quaternion.identity, quaternion3, value2) * quaternion;
                }
            }
            this.cam.transform.SetPositionAndRotation(this.smoothPos, quaternion);
            this.cam.fieldOfView = this.fov;
            this.cam.nearClipPlane = this.NearClip.Value;
        }

        private static Vector3 GetEyeMidpoint(ChaControl c, Transform headT)
        {
            EyeLookController eyeLookCtrl = c.eyeLookCtrl;
            if (eyeLookCtrl != null && eyeLookCtrl.eyeLookScript != null)
            {
                EyeObject[] eyeObjs = eyeLookCtrl.eyeLookScript.eyeObjs;
                if (eyeObjs != null && eyeObjs.Length >= 2 && eyeObjs[0] != null && eyeObjs[1] != null && eyeObjs[0].eyeTransform && eyeObjs[1].eyeTransform)
                {
                    return Vector3.Lerp(eyeObjs[0].eyeTransform.position, eyeObjs[1].eyeTransform.position, 0.5f);
                }
            }
            return headT.position + headT.rotation * new Vector3(0f, 0.06f, 0.08f);
        }

        private void EnablePov()
        {
            ChaControl chaControl = this.vrEntryTarget;
            this.vrEntryTarget = null;
            bool flag = this.IsVrEnabled() && this.FindVrRig(true);
            if (flag && this.IsVrModPovActive()) { base.Logger.LogMessage("Can't enter POV: the VR mod's own POV is active, turn that off first"); return; }
            Camera camera = (flag ? this.vrCam : Camera.main);
            if (!camera) { base.Logger.LogMessage("Can't enter POV: no main camera found"); return; }
            if (!chaControl) { chaControl = this.FindPovCharacter(); }
            if (!chaControl) { base.Logger.LogMessage(this.isStudio ? "Can't enter POV: select a character in the workspace first" : "Can't enter POV: no suitable character found"); return; }
            this.chara = chaControl;
            this.cam = camera;
            this.vrPov = flag;
            if (flag) { this.BeginVrPov(); }
            else
            {
                this.disabledCameraControl = this.cam.GetComponent<CameraControl_Ver2>();
                if (!this.disabledCameraControl) { this.disabledCameraControl = this.cam.GetComponent<global::Studio.CameraControl>(); }
                if (this.disabledCameraControl) { this.disabledCameraControl.enabled = false; }
                this.SetCursorLock(false);
                this.FreezeRoamCamera();
                this.dof = this.cam.GetComponent<DepthOfField>();
                if (this.dof && this.dof.enabled && this.dof.focalTransform)
                {
                    this.dofOrigSize = this.dof.focalSize;
                    this.dofOrigAperture = this.dof.aperture;
                    this.dofOrigFocalPos = this.dof.focalTransform.localPosition;
                    this.dof.focalTransform.localPosition = new Vector3(0f, 0f, 0.25f);
                    this.dof.focalSize = 0.9f;
                    this.dof.aperture = 0.6f;
                    this.dofChanged = true;
                }
            }
            this.origFov = this.cam.fieldOfView;
            this.origNearClip = this.cam.nearClipPlane;
            if (this.fov <= 0f) { this.fov = this.DefaultFov.Value; }
            this.ApplyHeadHiding();
            this.nextHeadRescan = Time.time + 0.5f;
            this.WarnAboutHiddenHeadsOnce();
            this.SuppressFreeRoamViewSwitcher();
            this.InitViewFromHead();
            this.manualRoll = 0f;
            this.camLocked = false;
            this.dragging = false;
            this.fpsMode = false;
            this.povEnabled = true;
        }

        private void DisablePov()
        {
            this.povEnabled = false;
            try
            {
                this.RestoreHeadRenderers();
                this.chara = null;
                if (this.cam)
                {
                    if (!this.vrPov) { this.cam.fieldOfView = this.origFov; }
                    this.cam.nearClipPlane = this.origNearClip;
                }
                this.RestoreVrRig();
                if (this.disabledCameraControl) { this.disabledCameraControl.enabled = true; }
                this.RestoreRoamCamera();
                this.RestoreFreeRoamViewSwitcher();
                if (this.dofChanged && this.dof)
                {
                    this.dof.focalSize = this.dofOrigSize;
                    this.dof.aperture = this.dofOrigAperture;
                    if (this.dof.focalTransform) { this.dof.focalTransform.localPosition = this.dofOrigFocalPos; }
                }
            }
            finally
            {
                this.SetCursorLock(false);
                this.chara = null;
                this.disabledCameraControl = null;
                this.dof = null;
                this.dofChanged = false;
                this.cam = null;
                this.vrPov = false;
                this.dragging = false;
                this.fpsMode = false;
                this.camLocked = false;
                this.vrFollowMode = 0;
                this.vrBaseValid = false;
                this.vrPrevFilteredDelta = Quaternion.identity;
                this.vrFollowButtonPrev = false;
            }
        }

        private void EnsureCameraConditionHooks()
        {
            if (this.hookedCameraControl) { return; }
            MonoBehaviour monoBehaviour = null;
            try
            {
                Camera main = Camera.main;
                if (!main) { return; }
                monoBehaviour = main.GetComponent<CameraControl_Ver2>();
                if (!monoBehaviour) { monoBehaviour = main.GetComponent<global::Studio.CameraControl>(); }
            }
            catch (Exception) { return; }
            if (!monoBehaviour) { return; }
            this.hookedCameraControl = monoBehaviour;
            this.noCtrlHook = this.HookCameraCondition(monoBehaviour, "noCtrlCondition", "CameraNoCtrlCondition");
            this.zoomHook = this.HookCameraCondition(monoBehaviour, "zoomCondition", "CameraZoomCondition");
        }

        private PerspectiveXPlugin.CameraCondHook HookCameraCondition(MonoBehaviour ctrl, string fieldName, string methodName)
        {
            PerspectiveXPlugin.CameraCondHook cameraCondHook;
            try
            {
                FieldInfo field = ctrl.GetType().GetField(fieldName, BindingFlags.IgnoreCase | BindingFlags.Instance | BindingFlags.Public);
                if (field == null || !typeof(Delegate).IsAssignableFrom(field.FieldType)) { cameraCondHook = null; }
                else
                {
                    MethodInfo method = base.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
                    Delegate @delegate = Delegate.CreateDelegate(field.FieldType, this, method, false);
                    if (@delegate == null) { cameraCondHook = null; }
                    else
                    {
                        PerspectiveXPlugin.CameraCondHook cameraCondHook2 = new PerspectiveXPlugin.CameraCondHook();
                        cameraCondHook2.Field = field;
                        cameraCondHook2.Prev = (Delegate)field.GetValue(ctrl);
                        cameraCondHook2.Ours = @delegate;
                        field.SetValue(ctrl, @delegate);
                        cameraCondHook = cameraCondHook2;
                    }
                }
            }
            catch (Exception) { cameraCondHook = null; }
            return cameraCondHook;
        }

        private void UnhookCameraConditions()
        {
            if (this.hookedCameraControl) { this.RestoreCameraCondition(this.noCtrlHook); this.RestoreCameraCondition(this.zoomHook); }
            this.noCtrlHook = null;
            this.zoomHook = null;
            this.hookedCameraControl = null;
        }

        private void RestoreCameraCondition(PerspectiveXPlugin.CameraCondHook hook)
        {
            if (hook == null) { return; }
            try
            {
                Delegate @delegate = (Delegate)hook.Field.GetValue(this.hookedCameraControl);
                hook.Field.SetValue(this.hookedCameraControl, (@delegate == hook.Ours) ? hook.Prev : Delegate.Remove(@delegate, hook.Ours));
            }
            catch (Exception) { }
        }

        private bool CameraNoCtrlCondition() { return PerspectiveXPlugin.InvokePrevCondition(this.noCtrlHook, false) || this.IsMouseOverPanel(); }
        private bool CameraZoomCondition() { return PerspectiveXPlugin.InvokePrevCondition(this.zoomHook, true) && !this.IsMouseOverPanel(); }

        private static bool InvokePrevCondition(PerspectiveXPlugin.CameraCondHook hook, bool fallback)
        {
            if (hook == null || hook.Prev == null) { return fallback; }
            bool flag;
            try { flag = (bool)hook.Prev.DynamicInvoke(null); } catch (Exception) { flag = fallback; }
            return flag;
        }

        private void FreezeRoamCamera()
        {
            this.roamCamSystem = null;
            this.roamCamShouldUpdate = null;
            try
            {
                Type type = Type.GetType("StrayTech.CameraSystem, Assembly-CSharp");
                if (type != null)
                {
                    global::UnityEngine.Object @object = global::UnityEngine.Object.FindObjectOfType(type);
                    if (@object)
                    {
                        PropertyInfo property = type.GetProperty("ShouldUpdate", BindingFlags.Instance | BindingFlags.Public);
                        if (property != null)
                        {
                            this.roamCamPrevShouldUpdate = (bool)property.GetValue(@object, null);
                            property.SetValue(@object, false, null);
                            this.roamCamSystem = @object;
                            this.roamCamShouldUpdate = property;
                        }
                    }
                }
            }
            catch (Exception) { this.roamCamSystem = null; this.roamCamShouldUpdate = null; }
        }

        private void RestoreRoamCamera()
        {
            if (this.roamCamShouldUpdate != null && this.roamCamSystem)
            {
                try { this.roamCamShouldUpdate.SetValue(this.roamCamSystem, this.roamCamPrevShouldUpdate, null); } catch (Exception) { }
            }
            this.roamCamSystem = null;
            this.roamCamShouldUpdate = null;
        }

        private bool IsFreeRoam() { return !this.isStudio && this.hFlag == null && this.FindFreeRoamViewSwitcher() != null; }

        private MonoBehaviour FindFreeRoamViewSwitcher()
        {
            MonoBehaviour monoBehaviour;
            try
            {
                Type type = Type.GetType("ActionGame.CameraStateDefinitionChange, Assembly-CSharp");
                if (type == null) { monoBehaviour = null; }
                else { monoBehaviour = global::UnityEngine.Object.FindObjectOfType(type) as MonoBehaviour; }
            }
            catch (Exception) { monoBehaviour = null; }
            return monoBehaviour;
        }

        private void SuppressFreeRoamViewSwitcher()
        {
            this.freeRoamViewSwitcher = null;
            MonoBehaviour monoBehaviour = this.FindFreeRoamViewSwitcher();
            if (!monoBehaviour) { return; }
            try
            {
                this.freeRoamViewSwitcherWasEnabled = monoBehaviour.enabled;
                monoBehaviour.enabled = false;
                this.freeRoamViewSwitcher = monoBehaviour;
                PerspectiveXPlugin.SetVisibleAll(this.chara, true);
            }
            catch (Exception) { }
        }

        private void RestoreFreeRoamViewSwitcher()
        {
            if (this.freeRoamViewSwitcher)
            {
                try { this.freeRoamViewSwitcher.enabled = this.freeRoamViewSwitcherWasEnabled; } catch (Exception) { }
            }
            this.freeRoamViewSwitcher = null;
        }

        private static void SetVisibleAll(ChaControl c, bool value)
        {
            if (!c) { return; }
            try
            {
                PropertyInfo property = c.GetType().GetProperty("visibleAll", BindingFlags.Instance | BindingFlags.Public);
                if (property != null && property.CanWrite) { property.SetValue(c, value, null); }
                else
                {
                    FieldInfo field = c.GetType().GetField("visibleAll", BindingFlags.Instance | BindingFlags.Public);
                    if (field == null) { return; }
                    field.SetValue(c, value);
                }
                c.LateUpdateForce();
            }
            catch (Exception) { }
        }

        private void CycleCharacter(int direction)
        {
            if (this.charaList == null || this.charaList.Count == 0) { this.RebuildCharaList(); }
            int count = this.charaList.Count;
            if (count == 0) { return; }
            int num = (this.chara ? this.charaList.IndexOf(this.chara) : (-1));
            for (int i = 1; i <= count; i++)
            {
                int num2 = ((num + direction * i) % count + count) % count;
                ChaControl chaControl = this.charaList[num2];
                if (!(chaControl == this.chara) && this.IsValidPovTarget(chaControl)) { this.SwitchTo(chaControl); return; }
            }
        }

        private void SwitchTo(ChaControl next)
        {
            this.RestoreHeadRenderers();
            this.chara = next;
            this.ApplyHeadHiding();
            if (this.freeRoamViewSwitcher) { PerspectiveXPlugin.SetVisibleAll(this.chara, true); }
            this.InitViewFromHead();
            this.camLocked = false;
            if (this.vrFollowMode != 0) { this.RecordVrFollowBase(); }
        }

        private void ApplyHeadHiding()
        {
            bool flag;
            if (this.vrPov) { flag = (this.isStudio ? this.HideHeadVrStudio.Value : this.HideHeadVrMain.Value); }
            else { flag = this.HideHead.Value; }
            if (flag) { this.HideHeadRenderers(); return; }
            this.RestoreHeadRenderers();
        }

        private void HideHeadRenderers()
        {
            if (!this.chara) { return; }
            this.deadRenderers.Clear();
            foreach (KeyValuePair<Renderer, bool> keyValuePair in this.hiddenRenderers) { if (!keyValuePair.Key) { this.deadRenderers.Add(keyValuePair.Key); } }
            for (int i = 0; i < this.deadRenderers.Count; i++) { this.hiddenRenderers.Remove(this.deadRenderers[i]); }
            this.deadRenderers.Clear();
            try
            {
                this.CollectHeadRenderers(this.chara.objHead);
                GameObject[] objHair = this.chara.objHair;
                if (objHair != null) { for (int j = 0; j < objHair.Length; j++) { this.CollectHeadRenderers(objHair[j]); } }
                this.CollectHeadRenderers(this.chara.objTongueEx);
                GameObject[] objAccessory = this.chara.objAccessory;
                ChaFileCoordinate nowCoordinate = this.chara.nowCoordinate;
                ChaFileAccessory.PartsInfo[] array;
                if (nowCoordinate == null) { array = null; }
                else { ChaFileAccessory accessory = nowCoordinate.accessory; array = ((accessory != null) ? accessory.parts : null); }
                ChaFileAccessory.PartsInfo[] array2 = array;
                if (objAccessory != null && array2 != null)
                {
                    int num = 0;
                    while (num < objAccessory.Length && num < array2.Length)
                    {
                        if (array2[num] != null && array2[num].partsOfHead) { this.CollectHeadRenderers(objAccessory[num]); }
                        num++;
                    }
                }
            }
            catch (Exception) { }
        }

        private void CollectHeadRenderers(GameObject root)
        {
            if (!root) { return; }
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer)
                {
                    if (!this.hiddenRenderers.ContainsKey(renderer)) { this.hiddenRenderers[renderer] = renderer.enabled; }
                    renderer.enabled = false;
                }
            }
        }

        private void RestoreHeadRenderers()
        {
            foreach (KeyValuePair<Renderer, bool> keyValuePair in this.hiddenRenderers)
            {
                Renderer key = keyValuePair.Key;
                if (key) { try { key.enabled = keyValuePair.Value; } catch (Exception) { } }
            }
            this.hiddenRenderers.Clear();
        }

        private int ScanHiddenHeads(bool repair)
        {
            int num = 0;
            foreach (ChaControl chaControl in global::UnityEngine.Object.FindObjectsOfType<ChaControl>())
            {
                if (chaControl)
                {
                    try
                    {
                        if (chaControl.fileStatus != null && !chaControl.fileStatus.visibleHeadAlways)
                        {
                            num++;
                            if (repair) { chaControl.fileStatus.visibleHeadAlways = true; chaControl.LateUpdateForce(); }
                        }
                    }
                    catch (Exception) { }
                }
            }
            return num;
        }

        private void DrawViewPresets(ConfigEntryBase entry)
        {
            if (GUILayout.Button("Cozy (60)", new GUILayoutOption[] { GUILayout.ExpandWidth(true) })) { this.ApplyViewPreset(60f, 0.65f, 0.05f); }
            if (GUILayout.Button("Natural (90)", new GUILayoutOption[] { GUILayout.ExpandWidth(true) })) { this.ApplyViewPreset(90f, 0.5f, 0.02f); }
            if (GUILayout.Button("Action (110)", new GUILayoutOption[] { GUILayout.ExpandWidth(true) })) { this.ApplyViewPreset(110f, 0.35f, 0f); }
        }

        private void ApplyViewPreset(float fovValue, float smoothing, float forwardOffset)
        {
            this.DefaultFov.Value = fovValue;
            this.PositionSmoothing.Value = smoothing;
            this.ForwardOffset.Value = forwardOffset;
        }

        private void SaveViewSlot(int slot)
        {
            this.viewSlots[slot].Value = string.Join(";", new string[] { this.fov.ToString(CultureInfo.InvariantCulture), this.yaw.ToString(CultureInfo.InvariantCulture), this.pitch.ToString(CultureInfo.InvariantCulture), this.manualRoll.ToString(CultureInfo.InvariantCulture), this.ForwardOffset.Value.ToString(CultureInfo.InvariantCulture), this.UpOffset.Value.ToString(CultureInfo.InvariantCulture) });
            base.Logger.LogMessage(string.Format("View saved to slot {0}", slot + 1));
        }

        private void LoadViewSlot(int slot)
        {
            string value = this.viewSlots[slot].Value;
            if (string.IsNullOrEmpty(value)) { base.Logger.LogMessage(string.Format("View slot {0} is empty - save the current view into it with {1}", slot + 1, this.saveSlotKeys[slot].Value)); return; }
            try
            {
                string[] array = value.Split(new char[] { ';' });
                this.fov = Mathf.Clamp(float.Parse(array[0], CultureInfo.InvariantCulture), 20f, 120f);
                this.yaw = float.Parse(array[1], CultureInfo.InvariantCulture);
                this.pitch = Mathf.Clamp(float.Parse(array[2], CultureInfo.InvariantCulture), -this.PitchLimit.Value, this.PitchLimit.Value);
                this.manualRoll = float.Parse(array[3], CultureInfo.InvariantCulture);
                this.ForwardOffset.Value = float.Parse(array[4], CultureInfo.InvariantCulture);
                this.UpOffset.Value = float.Parse(array[5], CultureInfo.InvariantCulture);
                base.Logger.LogMessage(string.Format("View slot {0} loaded", slot + 1));
            }
            catch (Exception) { base.Logger.LogMessage(string.Format("View slot {0} couldn't be read, save it again", slot + 1)); }
        }

        private void SetPanelVisible(bool visible)
        {
            if (this.panelVisible == visible) { return; }
            this.panelVisible = visible;
            if (!visible) { this.presetNameFocused = false; } else { this.nextHiddenHeadScan = 0f; }
            if (this.toolbarToggle == null || this.toolbarToggleValue == null) { return; }
            try { if ((bool)this.toolbarToggleValue.GetValue(this.toolbarToggle, null) != visible) { this.toolbarToggleValue.SetValue(this.toolbarToggle, visible, null); } } catch (Exception) { }
        }

        private bool IsMouseOverPanel()
        {
            if (!this.panelVisible || !this.panelRectInit) { return false; }
            Vector2 vector = new Vector2(Input.mousePosition.x, (float)Screen.height - Input.mousePosition.y);
            return this.panelRect.Contains(vector);
        }

        private void OnGUI()
        {
            if (!this.panelVisible) { this.presetNameFocused = false; return; }
            if (!this.panelStylesInit)
            {
                this.panelHeaderStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
                this.panelHintStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true };
                this.panelHintStyle.normal.textColor = new Color(1f, 1f, 1f, 0.65f);
                this.panelStylesInit = true;
            }
            if (!this.panelRectInit) { this.panelRect = new Rect(70f, Mathf.Max(40f, (float)Screen.height - 520f), 340f, 0f); this.panelRectInit = true; }
            this.panelRect = GUILayout.Window(48321, this.panelRect, new GUI.WindowFunction(this.DrawPanel), "PerspectiveX", new GUILayoutOption[] { GUILayout.Width(340f) });
            this.panelRect.x = Mathf.Clamp(this.panelRect.x, 0f, Mathf.Max(0f, (float)Screen.width - this.panelRect.width));
            this.panelRect.y = Mathf.Clamp(this.panelRect.y, 0f, Mathf.Max(0f, (float)Screen.height - this.panelRect.height));
        }

        private void DrawPanel(int windowId)
        {
            if (GUI.Button(new Rect(this.panelRect.width - 23f, 3f, 20f, 16f), "X")) { this.SetPanelVisible(false); return; }
            GUILayout.Label("View presets", this.panelHeaderStyle, new GUILayoutOption[0]);
            GUILayout.BeginHorizontal(new GUILayoutOption[0]);
            if (GUILayout.Button("Cozy (60)", new GUILayoutOption[0])) { this.ApplyViewPreset(60f, 0.65f, 0f); }
            if (GUILayout.Button("Natural (90)", new GUILayoutOption[0])) { this.ApplyViewPreset(90f, 0.5f, 0f); }
            if (GUILayout.Button("Action (110)", new GUILayoutOption[0])) { this.ApplyViewPreset(110f, 0.35f, 0f); }
            GUILayout.EndHorizontal();
            GUILayout.Space(8f);
            GUILayout.Label("Custom presets", this.panelHeaderStyle, new GUILayoutOption[0]);
            int num = -1;
            for (int i = 0; i < 5; i++)
            {
                string value = this.customPresets[i].Value;
                if (string.IsNullOrEmpty(value)) { if (num < 0) { num = i; } }
                else
                {
                    string[] array = value.Split(new char[] { ';' });
                    GUILayout.BeginHorizontal(new GUILayoutOption[0]);
                    GUILayout.Label(array[0], new GUILayoutOption[] { GUILayout.ExpandWidth(true) });
                    if (GUILayout.Button("Apply", new GUILayoutOption[] { GUILayout.Width(56f) })) { this.ApplyCustomPreset(array); }
                    if (GUILayout.Button("Update", new GUILayoutOption[] { GUILayout.Width(62f) })) { this.customPresets[i].Value = this.SerializeCurrentPreset(array[0]); }
                    if (GUILayout.Button("X", new GUILayoutOption[] { GUILayout.Width(26f) })) { this.customPresets[i].Value = ""; }
                    GUILayout.EndHorizontal();
                }
            }
            if (num >= 0)
            {
                GUILayout.BeginHorizontal(new GUILayoutOption[0]);
                GUI.SetNextControlName("PerspectiveXPresetName");
                this.newPresetName = GUILayout.TextField(this.newPresetName, 24, new GUILayoutOption[] { GUILayout.ExpandWidth(true) });
                if (GUILayout.Button("Save current", new GUILayoutOption[] { GUILayout.Width(96f) }))
                {
                    string text = this.newPresetName.Trim();
                    if (text.Length == 0) { text = "Preset " + (num + 1).ToString(); }
                    this.customPresets[num].Value = this.SerializeCurrentPreset(text);
                    this.newPresetName = "";
                    GUIUtility.keyboardControl = 0;
                }
                GUILayout.EndHorizontal();
                GUILayout.Label("Type a name and save the current FOV, smoothing and camera offsets as your own preset.", this.panelHintStyle, new GUILayoutOption[0]);
            }
            else { GUILayout.Label("All " + 5.ToString() + " custom slots are used - delete one (X) to add another.", this.panelHintStyle, new GUILayoutOption[0]); }
            GUILayout.Space(8f);
            GUILayout.Label("Saved views", this.panelHeaderStyle, new GUILayoutOption[0]);
            for (int j = 0; j < 3; j++)
            {
                bool flag = !string.IsNullOrEmpty(this.viewSlots[j].Value);
                GUILayout.BeginHorizontal(new GUILayoutOption[0]);
                GUILayout.Label("Slot " + (j + 1).ToString() + this.DescribeViewSlot(j, flag), new GUILayoutOption[] { GUILayout.ExpandWidth(true) });
                GUI.enabled = this.povEnabled;
                if (GUILayout.Button("Save", new GUILayoutOption[] { GUILayout.Width(56f) })) { this.SaveViewSlot(j); }
                GUI.enabled = this.povEnabled && flag;
                if (GUILayout.Button("Load", new GUILayoutOption[] { GUILayout.Width(56f) })) { this.LoadViewSlot(j); }
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            if (!this.povEnabled) { GUILayout.Label("Views store the exact look direction and zoom - enter POV (" + this.ToggleKey.Value.ToString() + ") to use them.", this.panelHintStyle, new GUILayoutOption[0]); }
            if (this.isStudio && this.hiddenHeadChars > 0)
            {
                GUILayout.Space(8f);
                GUILayout.Label("Scene repair", this.panelHeaderStyle, new GUILayoutOption[0]);
                if (GUILayout.Button("Bring back hidden heads (" + this.hiddenHeadChars.ToString() + ")", new GUILayoutOption[0]))
                {
                    int num2 = this.ScanHiddenHeads(true);
                    this.hiddenHeadChars = 0;
                    base.Logger.LogMessage((num2 > 0) ? ("Heads restored on " + num2.ToString() + " character(s) - save the scene to keep it") : "No hidden heads found");
                }
                GUILayout.Label("Versions up to 1.3.2 could save a hidden head into the scene file if you saved while in POV. This puts it back - save the scene afterwards.", this.panelHintStyle, new GUILayoutOption[0]);
            }
            this.presetNameFocused = GUI.GetNameOfFocusedControl() == "PerspectiveXPresetName";
            GUI.DragWindow();
        }

        private string DescribeViewSlot(int slot, bool hasData)
        {
            if (!hasData) { return "  (empty)"; }
            string text;
            try { string[] array = this.viewSlots[slot].Value.Split(new char[] { ';' }); text = "  (FOV " + Mathf.RoundToInt(float.Parse(array[0], CultureInfo.InvariantCulture)).ToString() + ")"; }
            catch (Exception) { text = ""; }
            return text;
        }

        private string SerializeCurrentPreset(string name)
        {
            float num = (this.povEnabled ? this.fov : this.DefaultFov.Value);
            return string.Join(";", new string[] { name.Replace(";", ","), num.ToString(CultureInfo.InvariantCulture), this.PositionSmoothing.Value.ToString(CultureInfo.InvariantCulture), this.ForwardOffset.Value.ToString(CultureInfo.InvariantCulture), this.UpOffset.Value.ToString(CultureInfo.InvariantCulture) });
        }

        private void ApplyCustomPreset(string[] parts)
        {
            try
            {
                this.ApplyViewPreset(Mathf.Clamp(float.Parse(parts[1], CultureInfo.InvariantCulture), 20f, 120f), float.Parse(parts[2], CultureInfo.InvariantCulture), float.Parse(parts[3], CultureInfo.InvariantCulture));
                if (parts.Length > 4) { this.UpOffset.Value = float.Parse(parts[4], CultureInfo.InvariantCulture); }
            }
            catch (Exception) { base.Logger.LogMessage("This preset couldn't be read, delete it and save it again"); }
        }

        private void TryAddStudioToolbarButton()
        {
            if (!this.isStudio) { return; }
            try
            {
                Type type = Type.GetType("KKAPI.Studio.UI.CustomToolbarButtons, KKAPI") ?? Type.GetType("KKAPI.Studio.UI.CustomToolbarButtons, KKSAPI");
                if (type != null)
                {
                    MethodInfo method = type.GetMethod("AddLeftToolbarToggle", BindingFlags.Static | BindingFlags.Public);
                    if (method != null)
                    {
                        this.toolbarToggle = method.Invoke(null, new object[] { PerspectiveXPlugin.MakePanelIcon(), false, new Action<bool>(this.SetPanelVisible) });
                        if (this.toolbarToggle != null) { this.toolbarToggleValue = this.toolbarToggle.GetType().GetProperty("Value", BindingFlags.Instance | BindingFlags.Public); }
                    }
                }
            }
            catch (Exception ex) { base.Logger.LogWarning("Couldn't add the Studio toolbar button (" + ex.Message + "), use the panel hotkey instead"); }
        }

        private static Texture2D MakePanelIcon()
        {
            Texture2D texture2D = new Texture2D(32, 32, TextureFormat.ARGB32, false);
            for (int i = 0; i < 32; i++)
            {
                int num = 31 - i;
                float num2 = Mathf.InverseLerp(13f, 19f, (float)num);
                float num3 = Mathf.Lerp(103f, 70f, num2) / 255f;
                float num4 = Mathf.Lerp(131f, 116f, num2) / 255f;
                for (int j = 0; j < 32; j++)
                {
                    bool flag = j == 0 || j == 31;
                    bool flag2 = num == 0 || num == 31;
                    float num5 = ((flag || flag2) ? num4 : num3);
                    float num6 = ((flag && flag2) ? 0.47843137f : ((flag || flag2) ? 0.9254902f : 1f));
                    float num13 = (float)j + 0.5f - 16f;
                    float num7 = (float)i + 0.5f - 16f;
                    float num8 = new Vector2(num13, num7 + 5.85f).magnitude - 13.35f;
                    float num9 = new Vector2(num13, num7 - 5.85f).magnitude - 13.35f;
                    float num10 = Mathf.Abs(Mathf.Max(num8, num9)) - 1.6f;
                    float num11 = Mathf.Sqrt(num13 * num13 + num7 * num7) - 3.9f;
                    float num12 = Mathf.Clamp01(0.5f - Mathf.Min(num10, num11));
                    num5 = Mathf.Lerp(num5, 1f, num12);
                    texture2D.SetPixel(j, i, new Color(num5, num5, num5, num6));
                }
            }
            texture2D.Apply();
            return texture2D;
        }

        private void InitViewFromHead()
        {
            if (this.chara.objHeadBone)
            {
                Vector3 forward = this.chara.objHeadBone.transform.forward;
                this.yaw = Mathf.Atan2(forward.x, forward.z) * 57.29578f;
                this.pitch = Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(forward.y, -1f, 1f)) * 57.29578f, -this.PitchLimit.Value, this.PitchLimit.Value);
            }
            this.smoothPosInit = false;
            this.swayInit = false;
            this.bodyRollInit = false;
            this.vrAlignPending = true;
        }

        private void RebuildCharaList()
        {
            this.charaList = (from c in global::UnityEngine.Object.FindObjectsOfType<ChaControl>() where c select c).ToList<ChaControl>();
        }

        private bool IsValidPovTarget(ChaControl c)
        {
            return c && c.objTop && c.objTop.activeInHierarchy && (c.sex != 0 || !this.hFlag || (this.hFlag.mode != HFlag.EMode.aibu && this.hFlag.mode != HFlag.EMode.lesbian && this.hFlag.mode != HFlag.EMode.masturbation));
        }

        private ChaControl FindPovCharacter()
        {
            if (this.isStudio)
            {
                List<OCIChar> list = (from x in Singleton<GuideObjectManager>.Instance.selectObjectKey select global::Studio.Studio.GetCtrlInfo(x) as OCIChar into x where x != null select x).ToList<OCIChar>();
                if (list.Count > 0) { return list[0].charInfo; }
            }
            this.RebuildCharaList();
            return this.charaList.FirstOrDefault(new Func<ChaControl, bool>(this.IsValidPovTarget));
        }

        private void SetCursorLock(bool locked)
        {
            this.cursorLocked = locked;
            try { if (Singleton<GameCursor>.IsInstance()) { Singleton<GameCursor>.Instance.SetCursorLock(locked); return; } } catch (Exception) { }
            Cursor.lockState = (locked ? CursorLockMode.Locked : CursorLockMode.None);
            Cursor.visible = !locked;
        }

        private void BindVrSettings()
        {
            this.VrSupportMain = base.Config.Bind<bool>("VR", "Use POV in VR (VR - Main game)", true, new ConfigDescription("In VR, POV puts your head at the character's eyes and you look around with the headset. Works with KK_MainGameVR, KKCharaStudioVR, KKS_VR and the official VR games. Off = POV works like older versions, flat screen only.", null, new object[] { new ConfigurationManagerAttributes { CustomDrawer = new Action<ConfigEntryBase>(this.DrawVrToggle), Order = new int?(7) } }));
            this.VrSupportStudio = base.Config.Bind<bool>("VR", "Use POV in VR (VR - Studio)", true, new ConfigDescription("In VR, POV puts your head at the character's eyes and you look around with the headset. Works with KK_MainGameVR, KKCharaStudioVR, KKS_VR and the official VR games. Off = POV works like older versions, flat screen only.", null, new object[] { new ConfigurationManagerAttributes { CustomDrawer = new Action<ConfigEntryBase>(this.DrawVrToggle), Order = new int?(6) } }));
            this.VrHeadEntry = base.Config.Bind<bool>("VR", "Enter POV by moving into a head", true, new ConfigDescription("Put your head inside a character's head for a moment to become them, lean or step out to leave. No keyboard needed. The POV toggle key still works too.", null, new object[] { new ConfigurationManagerAttributes { CustomDrawer = new Action<ConfigEntryBase>(this.DrawVrToggle), Order = new int?(5) } }));
            this.VrForwardOffset = base.Config.Bind<float>("VR", "Forward offset", 0.05f, new ConfigDescription("VR only. Headset distance in front of the eyes, in meters. Negative moves you back into the head. The flat-screen offset is under Camera.", new AcceptableValueRange<float>(-0.1f, 0.2f), new object[] { new ConfigurationManagerAttributes { Order = new int?(4) } }));
            this.VrUpOffset = base.Config.Bind<float>("VR", "Up offset", 0f, new ConfigDescription("VR only. Headset height above the eye line, in meters. The flat-screen offset is under Camera.", new AcceptableValueRange<float>(-0.1f, 0.1f), new object[] { new ConfigurationManagerAttributes { Order = new int?(3) } }));
            this.VrNearClip = base.Config.Bind<float>("VR", "Near clip plane", 0.01f, new ConfigDescription("VR only. Lower values let very close geometry render without being cut off. 0 = leave the VR mod's own setting alone.", new AcceptableValueRange<float>(0f, 0.1f), new object[] { new ConfigurationManagerAttributes { Order = new int?(2) } }));
            this.HideHeadVrMain = base.Config.Bind<bool>("VR", "Hide character head (VR - Main game)", true, new ConfigDescription("Main game VR only. Default on because the Main game VR mod does not hide the head.", null, new object[] { new ConfigurationManagerAttributes { Order = new int?(1) } }));
            this.HideHeadVrStudio = base.Config.Bind<bool>("VR", "Hide character head (VR - Studio)", false, new ConfigDescription("Studio VR only. Default off because KKCharaStudioVR hides the head itself; leaving this on may conflict.", null, new object[] { new ConfigurationManagerAttributes { Order = new int?(0) } }));
            try
            {
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) { this.NoteVrAssembly(assembly); }
                AppDomain.CurrentDomain.AssemblyLoad += this.OnAssemblyLoad;
                this.vrDetectionHooked = true;
            }
            catch (Exception) { }
        }

        private void StopVrDetection()
        {
            if (this.vrDetectionHooked) { AppDomain.CurrentDomain.AssemblyLoad -= this.OnAssemblyLoad; }
            this.vrDetectionHooked = false;
        }

        private void OnAssemblyLoad(object sender, AssemblyLoadEventArgs e) { this.NoteVrAssembly(e.LoadedAssembly); }

        private void NoteVrAssembly(Assembly asm)
        {
            try
            {
                string name = asm.GetName().Name;
                if (name.StartsWith("VRGIN", StringComparison.OrdinalIgnoreCase))
                {
                    Type type = asm.GetType("VRGIN.Core.VR", false);
                    if (type != null) { this.vrginTypes.Add(type); }
                }
                else if (name == "Assembly-CSharp") { this.officialVrRigType = asm.GetType("VRViveCameraManager", false); }
                else if (this.vrModPovActive == null)
                {
                    Type type2 = asm.GetType("KK_VR.Features.PoV", false);
                    if (type2 != null) { this.vrModPovActive = type2.GetProperty("Active", BindingFlags.Static | BindingFlags.Public); }
                }
            }
            catch (Exception) { }
        }

        private bool IsVrModInstalled()
        {
            if (this.vrginTypes.Count > 0 || this.officialVrRigType != null) { return true; }
            try
            {
                for (int i = 0; i < PerspectiveXPlugin.VrModGuids.Length; i++)
                {
                    if (Chainloader.PluginInfos.ContainsKey(PerspectiveXPlugin.VrModGuids[i])) { return true; }
                }
            }
            catch (Exception) { }
            return false;
        }

        private string RequiredVrModName()
        {
            if (!Application.unityVersion.StartsWith("5.", StringComparison.Ordinal)) { return "KKS_VR"; }
            if (!this.isStudio) { return "KK_MainGameVR"; }
            return "KKCharaStudioVR";
        }

        private void DrawVrToggle(ConfigEntryBase entry)
        {
            ConfigEntry<bool> configEntry = (ConfigEntry<bool>)entry;
            if (this.IsVrModInstalled()) { configEntry.Value = GUILayout.Toggle(configEntry.Value, configEntry.Value ? "Enabled" : "Disabled", new GUILayoutOption[] { GUILayout.ExpandWidth(true) }); return; }
            if (this.vrWarnStyle == null)
            {
                this.vrWarnStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
                this.vrWarnStyle.normal.textColor = new Color(1f, 0.35f, 0.35f);
            }
            GUILayout.BeginHorizontal(new GUILayoutOption[0]);
            bool enabled = GUI.enabled;
            GUI.enabled = false;
            GUILayout.Toggle(false, "Disabled", new GUILayoutOption[] { GUILayout.ExpandWidth(false) });
            GUI.enabled = enabled;
            GUILayout.Label(new GUIContent("! Needs " + this.RequiredVrModName(), "No VR mod found. Install " + this.RequiredVrModName() + " to use POV in VR."), this.vrWarnStyle, new GUILayoutOption[] { GUILayout.ExpandWidth(true) });
            GUILayout.EndHorizontal();
        }

        private bool FindVrRig(bool force)
        {
            if (this.vrOrigin && this.vrHead && this.vrCam) { return true; }
            if (this.vrginTypes.Count == 0 && this.officialVrRigType == null) { return false; }
            if (!force && Time.unscaledTime < this.nextVrRigSearch) { return false; }
            this.nextVrRigSearch = Time.unscaledTime + 1f;
            this.vrOrigin = null;
            this.vrHead = null;
            this.vrCam = null;
            try
            {
                for (int i = 0; i < this.vrginTypes.Count; i++) { if (this.TryVrginRig(this.vrginTypes[i])) { return true; } }
                if (this.officialVrRigType != null) { return this.TryOfficialVrRig(); }
            }
            catch (Exception) { }
            return false;
        }

        private bool TryVrginRig(Type vr)
        {
            PropertyInfo property = vr.GetProperty("Active", BindingFlags.Static | BindingFlags.Public);
            if (property == null || !(bool)property.GetValue(null, null)) { return false; }
            UnityEngine.Component component = vr.GetProperty("Camera", BindingFlags.Static | BindingFlags.Public).GetValue(null, null) as UnityEngine.Component;
            if (!component) { return false; }
            Type type = component.GetType();
            return this.SetVrRig(type.GetProperty("Origin", BindingFlags.Instance | BindingFlags.Public).GetValue(component, null) as Transform, type.GetProperty("Head", BindingFlags.Instance | BindingFlags.Public).GetValue(component, null) as Transform, component.GetComponent<Camera>());
        }

        private bool TryOfficialVrRig()
        {
            global::UnityEngine.Object @object = global::UnityEngine.Object.FindObjectOfType(this.officialVrRigType);
            if (!@object) { return false; }
            GameObject gameObject = this.officialVrRigType.GetField("objMove", BindingFlags.Instance | BindingFlags.Public).GetValue(@object) as GameObject;
            GameObject gameObject2 = this.officialVrRigType.GetField("objCamera", BindingFlags.Instance | BindingFlags.Public).GetValue(@object) as GameObject;
            return gameObject && gameObject2 && this.SetVrRig(gameObject.transform, gameObject2.transform, gameObject2.GetComponent<Camera>());
        }

        private bool SetVrRig(Transform origin, Transform head, Camera c)
        {
            if (!origin || !head || !c) { return false; }
            this.vrOrigin = origin;
            this.vrHead = head;
            this.vrCam = c;
            return true;
        }

        private bool IsVrModPovActive()
        {
            bool flag;
            try { flag = this.vrModPovActive != null && (bool)this.vrModPovActive.GetValue(null, null); } catch (Exception) { flag = false; }
            return flag;
        }

        private void UpdateVrHeadEntry()
        {
            if (!this.IsVrEnabled() || !this.VrHeadEntry.Value || !this.FindVrRig(false) || this.IsVrModPovActive()) { this.vrEnterCandidate = null; return; }
            if (this.charaList == null || Time.unscaledTime >= this.nextVrCharaScan) { this.nextVrCharaScan = Time.unscaledTime + 1f; this.RebuildCharaList(); }
            ChaControl chaControl = null;
            Vector3 position = this.vrHead.position;
            for (int i = 0; i < this.charaList.Count; i++)
            {
                ChaControl chaControl2 = this.charaList[i];
                if (this.IsValidPovTarget(chaControl2) && chaControl2.objHeadBone)
                {
                    Transform transform = chaControl2.objHeadBone.transform;
                    if ((PerspectiveXPlugin.GetEyeMidpoint(chaControl2, transform) - transform.forward * 0.06f - position).sqrMagnitude < 0.0081f) { chaControl = chaControl2; break; }
                }
            }
            if (chaControl != this.vrEnterCandidate) { this.vrEnterCandidate = chaControl; this.vrEnterSince = Time.unscaledTime; return; }
            if (chaControl && Time.unscaledTime - this.vrEnterSince >= 0.15f) { this.vrEnterCandidate = null; this.vrEntryTarget = chaControl; this.EnablePov(); }
        }

        private void BeginVrPov()
        {
            this.vrOrigPos = this.vrOrigin.position;
            this.vrOrigRot = this.vrOrigin.rotation;
            this.vrFollowInit = false;
            this.vrLeftHead = false;
            this.vrLastFrame = -1;
            this.vrEnterCandidate = null;
            this.vrFollowMode = 0;
            this.vrBaseValid = false;
            this.vrPrevFilteredDelta = Quaternion.identity;
            this.vrFollowLRef = Vector3.zero;
            this.vrFollowButtonPrev = false;
        }

        private bool CheckVrPovStillValid()
        {
            if (!this.vrOrigin || !this.vrHead || !this.vrCam) { return false; }
            if (this.IsVrModPovActive()) { base.Logger.LogMessage("The VR mod's own POV took over the headset, PerspectiveX POV turned off"); return false; }
            return !this.vrLeftHead;
        }

        private bool IsFirstVrCullThisFrame()
        {
            if (Time.frameCount == this.vrLastFrame || !this.vrOrigin || !this.vrHead) { return false; }
            this.vrLastFrame = Time.frameCount;
            return true;
        }

        private void PlaceVrRig()
        {
            if (this.vrAlignPending)
            {
                Vector3 forward = this.chara.objHeadBone.transform.forward;
                Vector3 forward2 = this.vrHead.forward;
                if (Mathf.Abs(forward.y) < 0.99f && Mathf.Abs(forward2.y) < 0.99f)
                {
                    float num = Mathf.DeltaAngle(Mathf.Atan2(forward2.x, forward2.z) * 57.29578f, Mathf.Atan2(forward.x, forward.z) * 57.29578f);
                    this.vrOrigin.RotateAround(this.vrHead.position, Vector3.up, num);
                }
                this.vrAlignPending = false;
                this.vrFollowInit = false;
                if (this.vrFollowMode != 0) { this.RecordVrFollowBase(); if (this.vrOrigin && this.vrHead) { this.vrFollowLRef = this.vrOrigin.InverseTransformPoint(this.vrHead.position); } }
            }
            if (this.vrFollowMode != 0 && this.vrBaseValid && this.chara && this.chara.objHeadBone && this.vrOrigin && this.vrHead)
            {
                Quaternion quaternion = this.chara.objHeadBone.transform.rotation * Quaternion.Inverse(this.vrBaseHeadRot);
                Quaternion quaternion2 = this.FilterByMode(quaternion, this.vrFollowMode);
                Quaternion quaternion4 = quaternion2 * Quaternion.Inverse(this.vrPrevFilteredDelta);
                Quaternion rotation = this.vrOrigin.rotation;
                Quaternion quaternion3 = quaternion4 * rotation;
                Vector3 vector = rotation * this.vrFollowLRef;
                Vector3 vector2 = quaternion3 * this.vrFollowLRef;
                this.vrOrigin.rotation = quaternion3;
                this.vrOrigin.position += vector - vector2;
                this.vrPrevFilteredDelta = quaternion2;
            }
            if (!this.vrFollowInit) { this.vrOrigin.position += this.smoothPos - this.vrHead.position; this.vrFollowInit = true; }
            else
            {
                this.vrOrigin.position += this.smoothPos - this.vrPrevTarget;
                if ((this.smoothPos - this.vrHead.position).sqrMagnitude > 0.122499995f) { this.vrLeftHead = true; }
            }
            this.vrPrevTarget = this.smoothPos;
            if (this.VrNearClip.Value > 0f) { this.vrCam.nearClipPlane = this.VrNearClip.Value; }
        }

        private void RestoreVrRig()
        {
            if (!this.vrPov || !this.vrOrigin) { return; }
            if (this.vrLeftHead) { this.vrLeftHead = false; base.Logger.LogMessage("Left POV"); return; }
            try { this.vrOrigin.SetPositionAndRotation(this.vrOrigPos, this.vrOrigRot); } catch (Exception) { }
        }

        public PerspectiveXPlugin() { }
        static PerspectiveXPlugin() { }

        private void RecordVrFollowBase()
        {
            if (!this.chara || !this.chara.objHeadBone || !this.vrOrigin) { this.vrBaseValid = false; return; }
            this.vrBaseHeadRot = this.chara.objHeadBone.transform.rotation;
            this.vrPrevFilteredDelta = Quaternion.identity;
            this.vrBaseValid = true;
        }

        private void CycleVrFollow()
        {
            int num = (this.vrFollowMode + 1) % 5;
            if (num == 0)
            {
                this.vrFollowMode = 0;
                this.vrPrevFilteredDelta = Quaternion.identity;
                base.Logger.LogMessage("VR rotation follow: position only");
                return;
            }
            if (!this.vrBaseValid)
            {
                this.RecordVrFollowBase();
                if (!this.vrBaseValid) { base.Logger.LogMessage("VR follow rotation: no character or VR rig"); return; }
            }
            if (this.chara && this.chara.objHeadBone && this.vrOrigin && this.vrHead)
            {
                Quaternion quaternion = this.chara.objHeadBone.transform.rotation * Quaternion.Inverse(this.vrBaseHeadRot);
                this.vrPrevFilteredDelta = this.FilterByMode(quaternion, num);
                this.vrFollowLRef = this.vrOrigin.InverseTransformPoint(this.vrHead.position);
            }
            else { this.vrPrevFilteredDelta = Quaternion.identity; }
            this.vrFollowMode = num;
            base.Logger.LogMessage("VR rotation follow: " + PerspectiveXPlugin.ModeName(num));
        }

        private Quaternion FilterByMode(Quaternion delta, int mode)
        {
            if (mode == 0) { return Quaternion.identity; }
            Vector3 eulerAngles = delta.eulerAngles;
            if (eulerAngles.x > 180f) { eulerAngles.x -= 360f; }
            if (eulerAngles.y > 180f) { eulerAngles.y -= 360f; }
            if (eulerAngles.z > 180f) { eulerAngles.z -= 360f; }
            if (mode == 1) { return Quaternion.Euler(0f, eulerAngles.y, 0f); }
            if (mode == 2) { return Quaternion.Euler(eulerAngles.x, 0f, 0f); }
            if (mode == 3) { return Quaternion.Euler(eulerAngles.x, eulerAngles.y, 0f); }
            return Quaternion.Euler(eulerAngles.x, eulerAngles.y, eulerAngles.z);
        }

        private static string ModeName(int mode)
        {
            switch (mode)
            {
                case 1: return "position + yaw";
                case 2: return "position + pitch";
                case 3: return "position + yaw + pitch";
                case 4: return "position + yaw + pitch + roll";
                default: return "position only";
            }
        }

        // ====== VR controller -> keyboard/mouse mapping (OpenVR) ======

        private void UpdateVrButtonMappings()
        {
            if (!this.EnsureOpenVr())
            {
                this.ResetAllMappingPrevStates();
                return;
            }

            ulong anyPressed = 0UL;
            string touchDir = "None";
            bool gotAny = false;

            for (uint i = 1; i < 16; i++)
            {
                object state = this.GetOpenVrState(i);
                if (state == null) { continue; }
                gotAny = true;
                try
                {
                    ulong p = (ulong)this._ulButtonPressedField.GetValue(state);
                    anyPressed |= p;

                    if ((p & (1UL << 32)) != 0UL && this._rAxis0Field != null)
                    {
                        object axis = this._rAxis0Field.GetValue(state);
                        if (axis != null)
                        {
                            Type axisType = axis.GetType();
                            FieldInfo xf = axisType.GetField("x");
                            FieldInfo yf = axisType.GetField("y");
                            if (xf != null && yf != null)
                            {
                                float x = (float)xf.GetValue(axis);
                                float y = (float)yf.GetValue(axis);
                                float mag = Mathf.Sqrt(x * x + y * y);
                                string d;
                                if (mag < 0.5f) { d = "Center"; }
                                else if (Mathf.Abs(y) > Mathf.Abs(x)) { d = y > 0 ? "Up" : "Down"; }
                                else { d = x > 0 ? "Right" : "Left"; }

                                if (d == "Up" || d == "Down" || d == "Left" || d == "Right") { touchDir = d; }
                                else if (d == "Center" && touchDir == "None") { touchDir = "Center"; }
                            }
                        }
                    }
                }
                catch (Exception) { }
            }

            if (!gotAny)
            {
                this.ResetAllMappingPrevStates();
                return;
            }

            this.ProcessBinaryMappingCombined(1UL << 33, this.VrMapTrigger.Value, this.vrMapTriggerPrev, anyPressed);
            this.ProcessBinaryMappingCombined(1UL << 2, this.VrMapGrip.Value, this.vrMapGripPrev, anyPressed);

            this.ApplyDirectionalEdge(touchDir == "Up", 0, this.VrMapUp.Value, this.vrMapUpPrev);
            this.ApplyDirectionalEdge(touchDir == "Down", 0, this.VrMapDown.Value, this.vrMapDownPrev);
            this.ApplyDirectionalEdge(touchDir == "Left", 0, this.VrMapLeft.Value, this.vrMapLeftPrev);
            this.ApplyDirectionalEdge(touchDir == "Right", 0, this.VrMapRight.Value, this.vrMapRightPrev);
            this.ApplyDirectionalEdge(touchDir == "Center", 0, this.VrMapCenter.Value, this.vrMapCenterPrev);
        }

        private void ProcessBinaryMappingCombined(ulong mask, VrMappedKey key, bool[] prevArray, ulong anyPressed)
        {
            if (key == VrMappedKey.None) { prevArray[0] = false; return; }
            bool isNow = (anyPressed & mask) != 0UL;
            bool was = prevArray[0];
            if (isNow && !was) { this.SimulateInput(key, true); }
            else if (!isNow && was) { this.SimulateInput(key, false); }
            prevArray[0] = isNow;
        }

        private void ResetAllMappingPrevStates()
        {
            for (int i = 0; i < 2; i++)
            {
                this.vrMapTriggerPrev[i] = false;
                this.vrMapGripPrev[i] = false;
                this.vrMapUpPrev[i] = false;
                this.vrMapDownPrev[i] = false;
                this.vrMapLeftPrev[i] = false;
                this.vrMapRightPrev[i] = false;
                this.vrMapCenterPrev[i] = false;
            }
        }

        private void ApplyDirectionalEdge(bool active, int handIndex, VrMappedKey key, bool[] prevArray)
        {
            bool flag = prevArray[handIndex];
            if (active && !flag) { this.SimulateInput(key, true); }
            else if (!active && flag) { this.SimulateInput(key, false); }
            prevArray[handIndex] = active;
        }

        private void SimulateInput(VrMappedKey key, bool down)
        {
            if (key == VrMappedKey.None) { return; }
            switch (key)
            {
                case VrMappedKey.LButton: PerspectiveXPlugin.mouse_event(down ? 2U : 4U, 0, 0, 0U, UIntPtr.Zero); return;
                case VrMappedKey.RButton: PerspectiveXPlugin.mouse_event(down ? 8U : 16U, 0, 0, 0U, UIntPtr.Zero); return;
                case VrMappedKey.MButton: PerspectiveXPlugin.mouse_event(down ? 32U : 64U, 0, 0, 0U, UIntPtr.Zero); return;
                case VrMappedKey.ScrollUp: if (down) { PerspectiveXPlugin.mouse_event(2048U, 0, 0, 120U, UIntPtr.Zero); } return;
                case VrMappedKey.ScrollDown: if (down) { PerspectiveXPlugin.mouse_event(2048U, 0, 0, 4294967176U, UIntPtr.Zero); } return;
            }

            byte b = PerspectiveXPlugin.VkCodeFor(key);
            if (b == 0) { return; }

            // 登记虚拟状态（让插件自己的快捷键能识别），同时保留 keybd_event 让游戏 UI 也能收到
            KeyCode kc = PerspectiveXPlugin.KeyCodeFor(key);
            if (kc != KeyCode.None)
            {
                if (down)
                {
                    if (!this.virtualHeldKeys.Contains(kc))
                    {
                        this.virtualHeldKeys.Add(kc);
                        this.virtualDownKeys.Add(kc);
                    }
                }
                else
                {
                    this.virtualHeldKeys.Remove(kc);
                }
            }

            uint scan = PerspectiveXPlugin.MapVirtualKey((uint)b, 0U);
            PerspectiveXPlugin.keybd_event(b, (byte)scan, down ? 0U : 2U, UIntPtr.Zero);
        }

        private static byte VkCodeFor(VrMappedKey key)
        {
            switch (key)
            {
                case VrMappedKey.Tab: return 9;
                case VrMappedKey.Return: return 13;
                case VrMappedKey.Escape: return 27;
                case VrMappedKey.Space: return 32;
                case VrMappedKey.Home: return 36;
                case VrMappedKey.End: return 35;
                case VrMappedKey.Left: return 37;
                case VrMappedKey.Up: return 38;
                case VrMappedKey.Right: return 39;
                case VrMappedKey.Down: return 40;
                case VrMappedKey.Insert: return 45;
                case VrMappedKey.Delete: return 46;
                case VrMappedKey.Prior: return 33;
                case VrMappedKey.Next: return 34;
                case VrMappedKey.Back: return 8;
                case VrMappedKey.Shift: return 16;
                case VrMappedKey.Control: return 17;
                case VrMappedKey.Menu: return 18;
                case VrMappedKey.Pause: return 19;
                case VrMappedKey.F1: return 112;
                case VrMappedKey.F2: return 113;
                case VrMappedKey.F3: return 114;
                case VrMappedKey.F4: return 115;
                case VrMappedKey.F5: return 116;
                case VrMappedKey.F6: return 117;
                case VrMappedKey.F7: return 118;
                case VrMappedKey.F8: return 119;
                case VrMappedKey.F9: return 120;
                case VrMappedKey.F10: return 121;
                case VrMappedKey.F11: return 122;
                case VrMappedKey.F12: return 123;
                case VrMappedKey.A: return 65;
                case VrMappedKey.B: return 66;
                case VrMappedKey.C: return 67;
                case VrMappedKey.D: return 68;
                case VrMappedKey.E: return 69;
                case VrMappedKey.F: return 70;
                case VrMappedKey.G: return 71;
                case VrMappedKey.H: return 72;
                case VrMappedKey.I: return 73;
                case VrMappedKey.J: return 74;
                case VrMappedKey.K: return 75;
                case VrMappedKey.L: return 76;
                case VrMappedKey.M: return 77;
                case VrMappedKey.N: return 78;
                case VrMappedKey.O: return 79;
                case VrMappedKey.P: return 80;
                case VrMappedKey.Q: return 81;
                case VrMappedKey.R: return 82;
                case VrMappedKey.S: return 83;
                case VrMappedKey.T: return 84;
                case VrMappedKey.U: return 85;
                case VrMappedKey.V: return 86;
                case VrMappedKey.W: return 87;
                case VrMappedKey.X: return 88;
                case VrMappedKey.Y: return 89;
                case VrMappedKey.Z: return 90;
                case VrMappedKey.D0: return 48;
                case VrMappedKey.D1: return 49;
                case VrMappedKey.D2: return 50;
                case VrMappedKey.D3: return 51;
                case VrMappedKey.D4: return 52;
                case VrMappedKey.D5: return 53;
                case VrMappedKey.D6: return 54;
                case VrMappedKey.D7: return 55;
                case VrMappedKey.D8: return 56;
                case VrMappedKey.D9: return 57;
                case VrMappedKey.NumPad0: return 96;
                case VrMappedKey.NumPad1: return 97;
                case VrMappedKey.NumPad2: return 98;
                case VrMappedKey.NumPad3: return 99;
                case VrMappedKey.NumPad4: return 100;
                case VrMappedKey.NumPad5: return 101;
                case VrMappedKey.NumPad6: return 102;
                case VrMappedKey.NumPad7: return 103;
                case VrMappedKey.NumPad8: return 104;
                case VrMappedKey.NumPad9: return 105;
                default: return 0;
            }
        }

        private static KeyCode KeyCodeFor(VrMappedKey key)
        {
            switch (key)
            {
                case VrMappedKey.A: return KeyCode.A;
                case VrMappedKey.B: return KeyCode.B;
                case VrMappedKey.C: return KeyCode.C;
                case VrMappedKey.D: return KeyCode.D;
                case VrMappedKey.E: return KeyCode.E;
                case VrMappedKey.F: return KeyCode.F;
                case VrMappedKey.G: return KeyCode.G;
                case VrMappedKey.H: return KeyCode.H;
                case VrMappedKey.I: return KeyCode.I;
                case VrMappedKey.J: return KeyCode.J;
                case VrMappedKey.K: return KeyCode.K;
                case VrMappedKey.L: return KeyCode.L;
                case VrMappedKey.M: return KeyCode.M;
                case VrMappedKey.N: return KeyCode.N;
                case VrMappedKey.O: return KeyCode.O;
                case VrMappedKey.P: return KeyCode.P;
                case VrMappedKey.Q: return KeyCode.Q;
                case VrMappedKey.R: return KeyCode.R;
                case VrMappedKey.S: return KeyCode.S;
                case VrMappedKey.T: return KeyCode.T;
                case VrMappedKey.U: return KeyCode.U;
                case VrMappedKey.V: return KeyCode.V;
                case VrMappedKey.W: return KeyCode.W;
                case VrMappedKey.X: return KeyCode.X;
                case VrMappedKey.Y: return KeyCode.Y;
                case VrMappedKey.Z: return KeyCode.Z;
                case VrMappedKey.D0: return KeyCode.Alpha0;
                case VrMappedKey.D1: return KeyCode.Alpha1;
                case VrMappedKey.D2: return KeyCode.Alpha2;
                case VrMappedKey.D3: return KeyCode.Alpha3;
                case VrMappedKey.D4: return KeyCode.Alpha4;
                case VrMappedKey.D5: return KeyCode.Alpha5;
                case VrMappedKey.D6: return KeyCode.Alpha6;
                case VrMappedKey.D7: return KeyCode.Alpha7;
                case VrMappedKey.D8: return KeyCode.Alpha8;
                case VrMappedKey.D9: return KeyCode.Alpha9;
                case VrMappedKey.F1: return KeyCode.F1;
                case VrMappedKey.F2: return KeyCode.F2;
                case VrMappedKey.F3: return KeyCode.F3;
                case VrMappedKey.F4: return KeyCode.F4;
                case VrMappedKey.F5: return KeyCode.F5;
                case VrMappedKey.F6: return KeyCode.F6;
                case VrMappedKey.F7: return KeyCode.F7;
                case VrMappedKey.F8: return KeyCode.F8;
                case VrMappedKey.F9: return KeyCode.F9;
                case VrMappedKey.F10: return KeyCode.F10;
                case VrMappedKey.F11: return KeyCode.F11;
                case VrMappedKey.F12: return KeyCode.F12;
                case VrMappedKey.Space: return KeyCode.Space;
                case VrMappedKey.Return: return KeyCode.Return;
                case VrMappedKey.Escape: return KeyCode.Escape;
                case VrMappedKey.Tab: return KeyCode.Tab;
                case VrMappedKey.Back: return KeyCode.Backspace;
                case VrMappedKey.Up: return KeyCode.UpArrow;
                case VrMappedKey.Down: return KeyCode.DownArrow;
                case VrMappedKey.Left: return KeyCode.LeftArrow;
                case VrMappedKey.Right: return KeyCode.RightArrow;
                case VrMappedKey.Home: return KeyCode.Home;
                case VrMappedKey.End: return KeyCode.End;
                case VrMappedKey.Insert: return KeyCode.Insert;
                case VrMappedKey.Delete: return KeyCode.Delete;
                case VrMappedKey.Prior: return KeyCode.PageUp;
                case VrMappedKey.Next: return KeyCode.PageDown;
                case VrMappedKey.Shift: return KeyCode.LeftShift;
                case VrMappedKey.Control: return KeyCode.LeftControl;
                case VrMappedKey.Menu: return KeyCode.LeftAlt;
                case VrMappedKey.Pause: return KeyCode.Pause;
                case VrMappedKey.NumPad0: return KeyCode.Keypad0;
                case VrMappedKey.NumPad1: return KeyCode.Keypad1;
                case VrMappedKey.NumPad2: return KeyCode.Keypad2;
                case VrMappedKey.NumPad3: return KeyCode.Keypad3;
                case VrMappedKey.NumPad4: return KeyCode.Keypad4;
                case VrMappedKey.NumPad5: return KeyCode.Keypad5;
                case VrMappedKey.NumPad6: return KeyCode.Keypad6;
                case VrMappedKey.NumPad7: return KeyCode.Keypad7;
                case VrMappedKey.NumPad8: return KeyCode.Keypad8;
                case VrMappedKey.NumPad9: return KeyCode.Keypad9;
                default: return KeyCode.None;
            }
        }

        // ====== OpenVR reflection ======

        private Type _openVrType;
        private object _vrSystem;
        private Type _controllerStateType;
        private FieldInfo _ulButtonPressedField;
        private FieldInfo _rAxis0Field;
        private bool _openVrReflectionDone;

        private bool EnsureOpenVr()
        {
            if (this._openVrReflectionDone) { return this._vrSystem != null; }
            this._openVrReflectionDone = true;
            try
            {
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type openVr = assembly.GetType("Valve.VR.OpenVR", false);
                    if (openVr == null) { continue; }
                    this._openVrType = openVr;
                    PropertyInfo systemProp = openVr.GetProperty("System", BindingFlags.Static | BindingFlags.Public);
                    if (systemProp == null) { return false; }
                    this._vrSystem = systemProp.GetValue(null, null);
                    if (this._vrSystem == null) { return false; }
                    this._controllerStateType = assembly.GetType("Valve.VR.VRControllerState_t", false);
                    if (this._controllerStateType == null) { return false; }
                    this._ulButtonPressedField = this._controllerStateType.GetField("ulButtonPressed");
                    if (this._ulButtonPressedField == null) { return false; }
                    this._rAxis0Field = this._controllerStateType.GetField("rAxis0");
                    return true;
                }
            }
            catch (Exception) { }
            return false;
        }

        private object GetOpenVrState(uint index)
        {
            if (!this.EnsureOpenVr()) { return null; }
            try
            {
                if (this._vrSystem == null) { return null; }
                MethodInfo getState = this._vrSystem.GetType().GetMethod("GetControllerState");
                if (getState == null) { return null; }
                object state = Activator.CreateInstance(this._controllerStateType);
                object[] args = new object[]
                {
                    index,
                    state,
                    (uint)System.Runtime.InteropServices.Marshal.SizeOf(this._controllerStateType)
                };
                bool ok = (bool)getState.Invoke(this._vrSystem, args);
                if (!ok) { return null; }
                return args[1];
            }
            catch (Exception) { }
            return null;
        }

        private bool ReadVrFollowButtonRaw()
        {
            VrFollowButton btn = this.VrFollowCycleButton.Value;
            if (btn == VrFollowButton.None) { return false; }
            ulong mask = this.ButtonMaskFor(btn);
            if (mask == 0UL) { return false; }
            if (!this.EnsureOpenVr()) { return false; }
            for (uint i = 1; i < 16; i++)
            {
                object state = this.GetOpenVrState(i);
                if (state == null) { continue; }
                try
                {
                    ulong pressed = (ulong)this._ulButtonPressedField.GetValue(state);
                    if ((pressed & mask) != 0UL) { return true; }
                }
                catch (Exception) { }
            }
            return false;
        }

        private ulong ButtonMaskFor(VrFollowButton btn)
        {
            switch (btn)
            {
                case VrFollowButton.Trigger: return 1UL << 33;
                case VrFollowButton.Grip: return 1UL << 2;
                case VrFollowButton.Touchpad: return 1UL << 32;
                case VrFollowButton.ApplicationMenu: return 1UL << 1;
                case VrFollowButton.A: return 1UL << 7;
                case VrFollowButton.B: return 1UL << 1;
                default: return 0UL;
            }
        }

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        public const string GUID = "bucky.kk.perspectivex";
        public const string PluginName = "PerspectiveX";
        public const string Version = "1.3.4";

        private const int ViewSlotCount = 3;
        private ConfigEntry<KeyboardShortcut>[] saveSlotKeys;
        private ConfigEntry<KeyboardShortcut>[] loadSlotKeys;
        private ConfigEntry<string>[] viewSlots;
        private const int CustomPresetCount = 5;
        private const int PanelWindowId = 48321;
        private const string PresetNameControl = "PerspectiveXPresetName";
        private const float HeadRescanInterval = 0.5f;
        private const float HiddenHeadScanInterval = 1f;
        private const int InputSuppressFrameLimit = 180;
        private ConfigEntry<string>[] customPresets;
        private bool panelVisible;
        private Rect panelRect;
        private bool panelRectInit;
        private string newPresetName = "";
        private bool presetNameFocused;
        private int inputSuppressFrames;
        private Vector3 lastSuppressMousePos;
        private int hiddenHeadChars;
        private float nextHiddenHeadScan;
        private GUIStyle panelHeaderStyle;
        private GUIStyle panelHintStyle;
        private bool panelStylesInit;
        private object toolbarToggle;
        private PropertyInfo toolbarToggleValue;
        private readonly bool isStudio = Paths.ProcessName == "CharaStudio";
        private bool povEnabled;
        private ChaControl chara;
        private List<ChaControl> charaList;
        private HFlag hFlag;
        private Camera cam;
        private MonoBehaviour disabledCameraControl;
        private MonoBehaviour hookedCameraControl;
        private PerspectiveXPlugin.CameraCondHook noCtrlHook;
        private PerspectiveXPlugin.CameraCondHook zoomHook;
        private global::UnityEngine.Object roamCamSystem;
        private PropertyInfo roamCamShouldUpdate;
        private bool roamCamPrevShouldUpdate;
        private MonoBehaviour freeRoamViewSwitcher;
        private bool freeRoamViewSwitcherWasEnabled;
        private DepthOfField dof;
        private bool dofChanged;
        private float dofOrigSize;
        private float dofOrigAperture;
        private Vector3 dofOrigFocalPos;
        private float origFov;
        private float origNearClip;
        private readonly Dictionary<Renderer, bool> hiddenRenderers = new Dictionary<Renderer, bool>();
        private readonly List<Renderer> deadRenderers = new List<Renderer>();
        private float nextHeadRescan;
        private bool hiddenHeadNoticeShown;
        private float yaw;
        private float pitch;
        private float fov;
        private float manualRoll;
        private bool camLocked;
        private float bodyRoll;
        private bool bodyRollInit;
        private bool dragging;
        private bool fpsMode;
        private bool cursorLocked;
        private Vector3 smoothPos;
        private bool smoothPosInit;
        private Quaternion swayBaseline;
        private bool swayInit;
        private const float VrEnterRadius = 0.09f;
        private const float VrEnterDwell = 0.5f;
        private const float VrExitDistance = 0.35f;
        private const float VrRecenterSpeed = 0.5f;
        private static readonly string[] VrModGuids = new string[] { "kk.vr.game", "kks.vr.game", "KKS_CharaStudioVR", "KKCharaStudioVRPlugin.KKCharaStudioVRPlugin" };
        private readonly List<Type> vrginTypes = new List<Type>();
        private Type officialVrRigType;
        private PropertyInfo vrModPovActive;
        private bool vrDetectionHooked;
        private GUIStyle vrWarnStyle;
        private Transform vrOrigin;
        private Transform vrHead;
        private Camera vrCam;
        private float nextVrRigSearch;
        private bool vrPov;
        private bool vrAlignPending;
        private bool vrFollowInit;
        private bool vrLeftHead;
        private Vector3 vrPrevTarget;
        private Vector3 vrOrigPos;
        private Quaternion vrOrigRot;
        private int vrLastFrame = -1;
        private ChaControl vrEntryTarget;
        private ChaControl vrEnterCandidate;
        private float vrEnterSince;
        private float nextVrCharaScan;
        private int vrFollowMode;
        private Quaternion vrBaseHeadRot;
        private bool vrBaseValid;
        private Quaternion vrPrevFilteredDelta = Quaternion.identity;
        private Vector3 vrFollowLRef;
        private bool vrFollowButtonPrev;
        private bool[] vrMapTriggerPrev = new bool[2];
        private bool[] vrMapGripPrev = new bool[2];
        private bool[] vrMapUpPrev = new bool[2];
        private bool[] vrMapDownPrev = new bool[2];
        private bool[] vrMapLeftPrev = new bool[2];
        private bool[] vrMapRightPrev = new bool[2];
        private bool[] vrMapCenterPrev = new bool[2];

        private sealed class CameraCondHook
        {
            public CameraCondHook() { }
            public FieldInfo Field;
            public Delegate Prev;
            public Delegate Ours;
        }
    }
}
