using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using LevelEditor;
using UnityEngine;

namespace EditorExtension.Patches;

[HarmonyPatch]
public static class SnapGridFix
{
    // --------------------------------------------------------------------------
    // 修复 Bug 1：UI 遮挡导致拖拽无法结束 (物理级兜底，不干涉原生吸附逻辑)
    // --------------------------------------------------------------------------
    [HarmonyPatch(typeof(LevelCreator), "Update")]
    [HarmonyPrefix]
    private static void LevelCreatorUpdatePrefix()
    {
        if (DynamicViewportManager.Instance != null)
        {
            DynamicViewportManager.Instance.TryUpdateCamera();
        }

        if (LevelCreator.Instance != null)
        {
            var traverse = Traverse.Create(LevelCreator.Instance);
            bool isDragging = traverse.Field<bool>("m_IsDragging").Value;

            // 只要逻辑上在拖拽，但物理上松开了左键，就强行结算。
            // 完美解决 Ground / 框选时鼠标移到 GUI 上导致状态机卡死、起点锁定的问题。
            if (isDragging && !Input.GetMouseButton(0))
            {
                traverse.Method("EndDrag").GetValue();
            }
        }
    }

    // --------------------------------------------------------------------------
    // 修复 Bug 2：从 Playtest 返回后的笔刷状态恢复与草皮错位
    // --------------------------------------------------------------------------
    [HarmonyPatch(typeof(LevelCreator), "OnPlayTestEnded")]
    [HarmonyPostfix]
    private static void LevelCreatorStopPlayTestingPostfix()
    {
        if (DynamicViewportManager.Instance != null)
        {
            DynamicViewportManager.Instance.OnPlayTestEnded();
        }

        if (LevelCreator.Instance != null)
        {
            LevelCreator.Instance.StartCoroutine(PlayTestEndedFixRoutine());
        }
    }

    private static IEnumerator PlayTestEndedFixRoutine()
    {
        // 等待 DynamicViewportManager 等逻辑执行完毕
        yield return new WaitForEndOfFrame();
        yield return null;

        var lc = LevelCreator.Instance;
        if (lc == null) yield break;

        var traverse = Traverse.Create(lc);
        // 1. 恢复笔刷 (Playtest 期间笔刷被隐藏)
        traverse.Method("MakeNewBrush").GetValue();

        // 2. 恢复网格和捕捉点
        bool showGrid = traverse.Field<bool>("m_ShowGrid").Value;
        lc.ShowGrid(showGrid);
        try { lc.GenerateSnapCornersFaces(); } catch { }

        // 3. 修复Ground视觉问题
        if (LevelManager.Instance != null && LevelManager.Instance.PlacedLevelObjects != null)
        {
            int count = LevelManager.Instance.NumberOfPlacedObjects;
            for (int i = 0; i < count; i++)
            {
                var lo = LevelManager.Instance.PlacedLevelObjects[i];
                if (lo != null && lo.Id != null && lo.Id.StartsWith("GROUND"))
                {
                    var traverseObj = Traverse.Create(lo);
                    GameObject attachedGround = traverseObj.Field<GameObject>("m_AttachedGround").Value;
                    if (attachedGround != null)
                    {
                        // 销毁旧的草皮网格
                        UnityEngine.Object.Destroy(attachedGround);

                        // 核心修复：在重新 InitGround 之前，利用记录的偏移量把物体位置恢复到下降前的高度
                        Vector3 currentPos = lo.VisibleObject.transform.position;
                        currentPos.x += lo.LevelObjectOffsetFromPosition.x;
                        currentPos.y += lo.LevelObjectOffsetFromPosition.y;
                        lo.VisibleObject.transform.position = currentPos;

                        // 重新生成草皮（此时会以正确的高度再次下降）
                        traverseObj.Method("InitGround").GetValue();
                        lo.UpdateGround();
                    }
                }
            }
        }
    }
}
