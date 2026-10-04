using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// 攻击动画判定事件校验（GDD §8.4 配套工具）。
    ///
    /// 目的：scan 出**哪些攻击动画漏挂了判定开启事件**。
    /// 因为 hitbox 系统靠动画事件开关判定体，漏挂事件的后果是
    /// "这个招式打出去**完全没有伤害**" —— 而且在编辑器里看不出来，只有实机打才发现。
    ///
    /// 用法：菜单 `Tools/Hitbox/校验攻击动画事件`
    /// </summary>
    public static class HitboxAnimationValidator
    {
        /// <summary>判定开启事件的函数名（玩家侧 / 敌人侧）。</summary>
        private static readonly string[] HitEventNames = new string[] { "OnAttackHit", "TickAttack" };

        /// <summary>扫描范围：项目里所有动画剪辑（.anim + FBX 内嵌）。</summary>
        private const string SearchRoot = "Assets/_Game/Art/Animations";

        [MenuItem("Tools/Hitbox/校验攻击动画事件")]
        public static void Validate()
        {
            List<AnimationClip> clips = CollectClips();
            if (clips.Count == 0)
            {
                EditorUtility.DisplayDialog("判定事件校验", "在 " + SearchRoot + " 下没找到任何动画剪辑。", "好");
                return;
            }

            StringBuilder report = new StringBuilder();
            int withEvent = 0, withoutEvent = 0;

            //按路径分组，便于阅读
            Dictionary<string, List<AnimationClip>> byPath = new Dictionary<string, List<AnimationClip>>();
            for (int i = 0; i < clips.Count; i++)
            {
                string p = AssetDatabase.GetAssetPath(clips[i]);
                if (!byPath.ContainsKey(p)) byPath[p] = new List<AnimationClip>();
                byPath[p].Add(clips[i]);
            }

            report.AppendLine("=== 攻击动画判定事件校验 ===");
            report.AppendLine("扫描范围: " + SearchRoot);
            report.AppendLine();

            foreach (KeyValuePair<string, List<AnimationClip>> pair in byPath)
            {
                report.AppendLine("【" + pair.Key + "】");
                for (int i = 0; i < pair.Value.Count; i++)
                {
                    AnimationClip clip = pair.Value[i];
                    List<string> hitEvents = GetHitEvents(clip);

                    if (hitEvents.Count > 0)
                    {
                        withEvent++;
                        report.AppendLine("  [有] " + clip.name + "   事件帧: " + string.Join(", ", hitEvents.ToArray()));
                    }
                    else
                    {
                        withoutEvent++;
                        report.AppendLine("  [无] " + clip.name + "   ⚠️ 该动画没有判定开启事件");
                    }
                }
                report.AppendLine();
            }

            report.AppendLine("=== 汇总 ===");
            report.AppendLine("  有判定事件: " + withEvent);
            report.AppendLine("  无判定事件: " + withoutEvent);
            report.AppendLine();
            report.AppendLine("提示：");
            report.AppendLine("  · 玩家普攻动画应挂 OnAttackHit（在 combo_01_1~4 上）");
            report.AppendLine("  · 敌人攻击动画应挂 TickAttack（在 combo_01_1 上）");
            report.AppendLine("  · 移动/待机/受击/死亡动画不需要判定事件，可忽略");

            Debug.Log(report.ToString());
        }

        private static List<AnimationClip> CollectClips()
        {
            List<AnimationClip> result = new List<AnimationClip>();

            if (!AssetDatabase.IsValidFolder(SearchRoot))
            {
                Debug.LogWarning("[HitboxAnimationValidator] 目录不存在: " + SearchRoot);
                return result;
            }

            string[] guids = AssetDatabase.FindAssets("t:AnimationClip", new string[] { SearchRoot });
            for (int i = 0; i < guids.Length; i++)
            {
                string p = AssetDatabase.GUIDToAssetPath(guids[i]);

                //一个 FBX 可能含多个 clip，逐个取出
                Object[] all = AssetDatabase.LoadAllAssetsAtPath(p);
                for (int j = 0; j < all.Length; j++)
                {
                    AnimationClip c = all[j] as AnimationClip;
                    if (c == null) continue;
                    if (c.name.StartsWith("__preview__")) continue;//预览用剪，跳过
                    if (!result.Contains(c)) result.Add(c);
                }
            }
            return result;
        }

        /// <summary>返回该动画里所有"判定开启事件"的描述（函数名@时间）。</summary>
        private static List<string> GetHitEvents(AnimationClip clip)
        {
            List<string> found = new List<string>();

            AnimationEvent[] events = AnimationUtility.GetAnimationEvents(clip);
            if (events == null) return found;

            for (int i = 0; i < events.Length; i++)
            {
                for (int k = 0; k < HitEventNames.Length; k++)
                {
                    if (events[i].functionName == HitEventNames[k])
                    {
                        found.Add(events[i].functionName + "@" + events[i].time.ToString("F2") + "s");
                        break;
                    }
                }
            }
            return found;
        }
    }
}
