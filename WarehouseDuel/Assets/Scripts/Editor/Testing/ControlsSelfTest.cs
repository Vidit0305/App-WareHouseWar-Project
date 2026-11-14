using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using WarehouseWar.EditorTools.UI;
using WarehouseWar.UI.Controls;

namespace WarehouseWar.EditorTools.Testing
{
    /// <summary>
    /// Automated check of the touch controls, built with the real HUD builder:
    ///  - no two buttons (or a button and the joystick) overlap,
    ///  - the joystick appears under the thumb, reports the drag direction, honours the dead zone and resets,
    ///  - the same holds in the mirrored (left-handed) layout.
    ///   Unity.exe -batchmode -nographics -projectPath WarehouseWar -executeMethod WarehouseWar.EditorTools.Testing.ControlsSelfTest.RunBatch
    /// </summary>
    public static class ControlsSelfTest
    {
        public static void RunBatch()
        {
            SetupReport report = Run();
            report.PrintToConsole("Controls self-test");
            EditorApplication.Exit(report.ErrorCount == 0 ? 0 : 1);
        }

        internal static SetupReport Run()
        {
            var report = new SetupReport();
            CheckLayout(report);

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var eventSystem = new GameObject("EventSystem").AddComponent<EventSystem>();

            // World-space canvas at 1:1 scale so canvas units == world units (independent of the batch-mode screen size).
            Canvas canvas = UiFactory.CreateCanvas("Test Canvas", 0, out RectTransform safe);
            canvas.renderMode = RenderMode.WorldSpace;
            var canvasRect = (RectTransform)canvas.transform;
            canvasRect.sizeDelta = new Vector2(1920f, 1080f);
            canvasRect.position = new Vector3(960f, 540f, 0f);
            canvasRect.localScale = Vector3.one;

            GameObject controls = HudBuilder.BuildTouchControls(safe);
            Canvas.ForceUpdateCanvases();
            VirtualJoystick joystick = controls.GetComponentInChildren<VirtualJoystick>(true);

            TestJoystick(report, joystick, eventSystem, "standard layout");

            // Mirror exactly as TouchControlsView does for the left-handed layout.
            var zone = (RectTransform)joystick.transform;
            zone.localScale = new Vector3(-1f, 1f, 1f);
            foreach (RectTransform child in zone) child.localScale = new Vector3(-Mathf.Abs(child.localScale.x), child.localScale.y, child.localScale.z);
            Canvas.ForceUpdateCanvases();
            TestJoystick(report, joystick, eventSystem, "left-handed layout");

            return report;
        }

        private static void CheckLayout(SetupReport report)
        {
            var items = HudBuilder.ButtonLayout;
            int overlaps = 0;
            for (int i = 0; i < items.Length; i++)
            {
                for (int j = i + 1; j < items.Length; j++)
                {
                    if (items[i].left != items[j].left) continue;
                    float gap = Vector2.Distance(items[i].position, items[j].position) - (items[i].size + items[j].size) / 2f;
                    if (gap < 10f) { overlaps++; report.Error($"Buttons '{items[i].name}' and '{items[j].name}' overlap (gap {gap:0} px)."); }
                }
                if (items[i].left)
                {
                    var stick = new Vector2(HudBuilder.StickRest, HudBuilder.StickRest - 20f);
                    float gap = Vector2.Distance(items[i].position, stick) - (items[i].size + HudBuilder.StickSize) / 2f;
                    if (gap < 10f) { overlaps++; report.Error($"Button '{items[i].name}' overlaps the joystick (gap {gap:0} px)."); }
                }
            }
            if (overlaps == 0) report.Pass($"Touch layout: {items.Length} buttons + joystick, no overlaps.");
        }

        private static void TestJoystick(SetupReport report, VirtualJoystick joystick, EventSystem eventSystem, string label)
        {
            var zone = (RectTransform)joystick.transform;
            var corners = new Vector3[4];
            zone.GetWorldCorners(corners);
            float minX = Mathf.Min(corners[0].x, corners[2].x), maxX = Mathf.Max(corners[0].x, corners[2].x);
            Vector2 touch = new Vector2(Mathf.Lerp(minX, maxX, 0.45f), Mathf.Lerp(corners[0].y, corners[2].y, 0.45f));

            var stickBase = (RectTransform)new SerializedObject(joystick).FindProperty("stickBase").objectReferenceValue;
            float radius = new SerializedObject(joystick).FindProperty("radius").floatValue * Mathf.Abs(stickBase.lossyScale.x);

            var e = new PointerEventData(eventSystem) { pointerId = 3, position = touch };
            joystick.OnPointerDown(e);
            float placementError = Vector2.Distance(stickBase.position, touch);
            Check(report, placementError < 1f, $"{label}: stick appears under the thumb (error {placementError:0.0} px)");

            e.position = touch + Vector2.right * radius;
            joystick.OnDrag(e);
            Check(report, joystick.Value.x > 0.9f && Mathf.Abs(joystick.Value.y) < 0.1f, $"{label}: drag right → {joystick.Value}");

            e.position = touch + Vector2.up * radius * 2f;
            joystick.OnDrag(e);
            Check(report, joystick.Value.y > 0.9f && Mathf.Abs(joystick.Value.x) < 0.1f, $"{label}: drag up (past the edge) → {joystick.Value}");

            e.position = touch + new Vector2(-1f, -1f).normalized * radius;
            joystick.OnDrag(e);
            Check(report, joystick.Value.x < -0.6f && joystick.Value.y < -0.6f, $"{label}: drag down-left → {joystick.Value}");

            e.position = touch + Vector2.right * radius * 0.05f;
            joystick.OnDrag(e);
            Check(report, joystick.Value == Vector2.zero, $"{label}: tiny movement inside the dead zone → {joystick.Value}");

            joystick.OnPointerUp(e);
            Check(report, joystick.Value == Vector2.zero && !joystick.IsActive, $"{label}: release resets the stick");
        }

        private static void Check(SetupReport report, bool ok, string message)
        {
            if (ok) report.Pass(message);
            else report.Error(message);
        }
    }
}
