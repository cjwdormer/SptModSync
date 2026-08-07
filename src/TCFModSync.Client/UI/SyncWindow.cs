using System;
using System.Collections.Generic;
using System.Linq;
using TCFModSync.Client.Sync;
using TCFModSync.Shared.Diffing;
using TCFModSync.Shared.Models;
using UnityEngine;

namespace TCFModSync.Client.UI
{
    public enum SyncWindowMode
    {
        Offer,
        Downloading,
        ConfirmRestart,
        Failed
    }

    public sealed class SyncWindow
    {
        private const int WindowId = 918273;

        public List<DiffResult> Diff { get; private set; } = new List<DiffResult>();
        public HashSet<string> Accepted { get; private set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public Action? OnAccept { get; set; }
        public Action? OnClose { get; set; }
        public Action<string> Log { get; set; } = _ => { };

        public Action? OnConfirmRestart { get; set; }
        public Action? OnDeclineRestart { get; set; }
        public Action? OnResetExclusions { get; set; }
        public int ExcludedCount { get; set; }
        public string ServerSptVersion { get; set; } = "";

        public SyncWindowMode Mode { get; private set; } = SyncWindowMode.Offer;

        public SyncProgress? Progress { get; set; }

        public bool Visible { get; private set; }

        private const float MinWindowWidth = 400f;
        private const float MinWindowHeight = 300f;
        private const float MaxWindowWidth = 900f;
        private const float MaxWindowHeight = 700f;

        private Vector2 _scroll;
        private Rect _windowRect = new Rect(0, 0, 700, 500);
        private int _positionedForWidth;
        private int _positionedForHeight;

        private static Texture2D? _panelTexture;
        private static Texture2D? _dimTexture;
        private static GUIStyle? _windowStyle;
        private bool _cursorForced;
        private CursorLockMode _previousLockState;
        private bool _previousCursorVisible;

        public void Open(List<DiffResult> diff)
        {
            Diff = diff;
            Accepted = new HashSet<string>(
                diff.Where(d => d.UserCanDecline).Select(d => d.RelativePath),
                StringComparer.OrdinalIgnoreCase);
            Mode = SyncWindowMode.Offer;
            Visible = true;
            Log($"[TCF-ModSync] Sync window opened with {diff.Count} item(s).");
        }

        public void ShowDownloading(SyncProgress progress)
        {
            Progress = progress;
            Mode = SyncWindowMode.Downloading;
            Visible = true;
        }

        public void Close()
        {
            Visible = false;
            RestoreCursor();
            GameInputBlocker.Unblock(Log);
        }

        public void ShowConfirmRestart()
        {
            Mode = SyncWindowMode.ConfirmRestart;
            Visible = true;
        }

        public void ShowError(string message)
        {
            ErrorMessage = message;
            Mode = SyncWindowMode.Failed;
            Visible = true;
        }

        public string ErrorMessage { get; private set; } = "";

        public void Draw()
        {
            if (!Visible)
            {
                RestoreCursor();
                GameInputBlocker.Unblock(Log);
                return;
            }

            GameInputBlocker.Block(Log);

            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), EnsureDimTexture());

            if (!_cursorForced)
            {
                _previousLockState = Cursor.lockState;
                _previousCursorVisible = Cursor.visible;
                _cursorForced = true;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            GUI.depth = -1000;

            CentreIfNeeded();

            _windowRect = GUILayout.Window(
                WindowId,
                _windowRect,
                DrawWindow,
                "SPT Mod Sync",
                EnsureWindowStyle(),
                GUILayout.Width(_windowRect.width),
                GUILayout.Height(_windowRect.height));

            _windowRect.width = Mathf.Clamp(_windowRect.width, MinWindowWidth, MaxWindowWidth);
            _windowRect.height = Mathf.Clamp(_windowRect.height, MinWindowHeight, MaxWindowHeight);
        }

        private void RestoreCursor()
        {
            if (!_cursorForced) return;
            Cursor.lockState = _previousLockState;
            Cursor.visible = _previousCursorVisible;
            _cursorForced = false;
        }

        private static GUIStyle EnsureWindowStyle()
        {
            if (_windowStyle != null && _panelTexture != null) return _windowStyle;

            EnsurePanelTexture();
            _windowStyle = new GUIStyle(GUI.skin.window);
            _windowStyle.normal.background = _panelTexture;
            _windowStyle.onNormal.background = _panelTexture;
            _windowStyle.focused.background = _panelTexture;
            _windowStyle.onFocused.background = _panelTexture;
            return _windowStyle;
        }

        private static Texture2D EnsurePanelTexture()
        {
            if (_panelTexture != null) return _panelTexture;
            _panelTexture = MakeTexture(new Color(0.29f, 0.30f, 0.32f, 1f));
            return _panelTexture;
        }

        private static Texture2D EnsureDimTexture()
        {
            if (_dimTexture != null) return _dimTexture;
            _dimTexture = MakeTexture(new Color(0f, 0f, 0f, 0.55f));
            return _dimTexture;
        }

        private static Texture2D MakeTexture(Color colour)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, colour);
            texture.Apply();
            texture.hideFlags = HideFlags.HideAndDontSave;
            return texture;
        }

        private void CentreIfNeeded()
        {
            if (_positionedForWidth == Screen.width && _positionedForHeight == Screen.height) return;

            var width = Mathf.Min(760f, Screen.width * 0.8f);
            var height = Mathf.Min(560f, Screen.height * 0.8f);

            _windowRect = new Rect(
                (Screen.width - width) / 2f,
                (Screen.height - height) / 2f,
                width,
                height);

            _positionedForWidth = Screen.width;
            _positionedForHeight = Screen.height;
        }

        private void DrawWindow(int id)
        {
            GUI.DrawTexture(new Rect(0f, 0f, _windowRect.width, _windowRect.height), EnsurePanelTexture());

            switch (Mode)
            {
                case SyncWindowMode.Downloading:
                    DrawDownloading();
                    GUI.DragWindow();
                    return;
                case SyncWindowMode.ConfirmRestart:
                    DrawConfirmRestart();
                    GUI.DragWindow();
                    return;
                case SyncWindowMode.Failed:
                    DrawFailed();
                    GUI.DragWindow();
                    return;
            }

            var bookkeeping = Diff
                .Where(d => d.Action == FileAction.Adopt || d.Action == FileAction.Untrack)
                .ToList();
            var actionable = Diff
                .Where(d => d.Action != FileAction.Adopt && d.Action != FileAction.Untrack)
                .ToList();

            var downloadBytes = actionable
                .Where(d => d.Action == FileAction.Add || d.Action == FileAction.Update)
                .Sum(d => d.Size ?? 0);

            if (!string.IsNullOrWhiteSpace(ServerSptVersion))
            {
                GUILayout.Label($"Server is running SPT {ServerSptVersion}.");
                GUILayout.Space(4);
            }

            GUILayout.Label($"{actionable.Count} change(s) to apply, {downloadBytes / 1024.0 / 1024.0:F1} MB to download.");
            if (bookkeeping.Count > 0)
            {
                GUILayout.Label($"{bookkeeping.Count} file(s) need no changes - records will just be brought up to date.");
            }

            GUILayout.Space(6);

            if (actionable.Count == 0)
            {
                GUILayout.Label("Nothing needs downloading - accepting will just record what you already have.");
            }

            var listHeight = Mathf.Max(120f, _windowRect.height - 160f);
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(listHeight));

            foreach (var item in actionable)
            {
                GUILayout.BeginHorizontal();

                if (item.UserCanDecline)
                {
                    var isChecked = Accepted.Contains(item.RelativePath);
                    var newChecked = GUILayout.Toggle(isChecked, "", GUILayout.Width(20));
                    if (newChecked != isChecked)
                    {
                        if (newChecked) Accepted.Add(item.RelativePath);
                        else Accepted.Remove(item.RelativePath);
                    }
                }
                else
                {
                    GUILayout.Label("!", GUILayout.Width(20));
                }

                GUILayout.Label($"[{item.Action}]", GUILayout.Width(70));

                var sizeText = item.Size.HasValue && item.Size.Value > 0
                    ? $"  ({item.Size.Value / 1024.0 / 1024.0:F1} MB)"
                    : "";
                GUILayout.Label(item.RelativePath + sizeText);
                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();

            if (ExcludedCount > 0)
            {
                GUILayout.Space(4);
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{ExcludedCount} file(s) hidden because you declined them previously.");
                if (GUILayout.Button("Offer Them Again", GUILayout.Width(150)))
                {
                    OnResetExclusions?.Invoke();
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(4);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Accept Offer", GUILayout.Height(32)))
            {
                Visible = false;
                RestoreCursor();
                OnAccept?.Invoke();
            }
            if (GUILayout.Button("Skip For Now", GUILayout.Height(32)))
            {
                Visible = false;
                RestoreCursor();
                GameInputBlocker.Unblock(Log);
                OnClose?.Invoke();
            }
            GUILayout.EndHorizontal();

            GUI.DragWindow();
        }

        private void DrawDownloading()
        {
            var p = Progress;
            if (p == null)
            {
                GUILayout.Label("Preparing download...");
                return;
            }

            GUILayout.Label($"Downloading {p.FilesDone} of {p.FilesTotal} file(s)");
            GUILayout.Label($"{p.BytesDone / 1024.0 / 1024.0:F1} MB of {p.BytesTotal / 1024.0 / 1024.0:F1} MB");

            var fraction = p.BytesTotal > 0 ? (float)((double)p.BytesDone / p.BytesTotal) : 0f;
            GUILayout.Label($"{fraction * 100f:F0}% complete");
            DrawProgressBar(fraction);

            GUILayout.Space(4);
            GUILayout.Label(string.IsNullOrEmpty(p.CurrentFile) ? " " : p.CurrentFile);

            GUILayout.Space(8);
            if (p.Complete)
            {
                GUILayout.Label("All files staged. Preparing to apply...");
            }
            else
            {
                GUILayout.Label("Files are being downloaded to a temporary staging folder. Nothing in " +
                                "your game has changed yet, and the game will not close until every " +
                                "file has been stored successfully.");
            }

            if (!string.IsNullOrEmpty(p.Error))
            {
                GUILayout.Space(6);
                GUILayout.Label($"Error: {p.Error}");
            }
        }

        private void DrawConfirmRestart()
        {
            GUILayout.Label("Download complete.");
            GUILayout.Space(6);
            GUILayout.Label("The game will now CLOSE so the files can be put into place.");
            GUILayout.Space(4);
            GUILayout.Label("It will NOT reopen by itself - start it again from the SPT launcher " +
                            "once it has shut down. This normally takes only a few seconds.");
            GUILayout.Space(6);
            GUILayout.Label("If you would rather not do this now, the downloaded files will be " +
                            "discarded and offered again next time you launch.");

            GUILayout.FlexibleSpace();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Close Game And Apply", GUILayout.Height(32)))
            {
                Visible = false;
                RestoreCursor();
                GameInputBlocker.Unblock(Log);
                OnConfirmRestart?.Invoke();
            }
            if (GUILayout.Button("Not Now", GUILayout.Height(32)))
            {
                Visible = false;
                RestoreCursor();
                GameInputBlocker.Unblock(Log);
                OnDeclineRestart?.Invoke();
            }
            GUILayout.EndHorizontal();
        }

        private void DrawFailed()
        {
            GUILayout.Label("Sync could not be completed.");
            GUILayout.Space(6);

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(Mathf.Max(80f, _windowRect.height - 200f)));
            GUILayout.Label(ErrorMessage);
            GUILayout.EndScrollView();

            GUILayout.Space(6);
            GUILayout.Label("Nothing in your game has been changed. Any partly downloaded files have " +
                            "been discarded, and the sync will be offered again next time you launch.");

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Continue Without Syncing", GUILayout.Height(32)))
            {
                Close();
                OnClose?.Invoke();
            }
        }

        private static void DrawProgressBar(float fraction)
        {
            fraction = Mathf.Clamp01(fraction);
            var rect = GUILayoutUtility.GetRect(100, 22, GUILayout.ExpandWidth(true));
            GUI.Box(rect, GUIContent.none);

            if (fraction > 0f)
            {
                var fill = new Rect(rect.x + 2, rect.y + 2, (rect.width - 4) * fraction, rect.height - 4);
                GUI.Box(fill, GUIContent.none);
            }
        }
    }
}
