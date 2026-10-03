using System.Collections.Generic;//w4s1
using UnityEngine;

namespace Modules.SerializationLab   // _Modules 的实验，命名空间带 Modules 提醒你别拿进 Gameplay
{
    public class SerializationProbe : MonoBehaviour
    {
        // 实验1：公有【属性】。属性是"方法变装"，Unity 的序列化器只认字段 → 它不会出现在 Inspector
        public int HpProperty { get; set; }

        // 实验2：公有【字段】→ 会出现在 Inspector
        public int hpPublicField = 100;

        // 实验3：私有【字段】默认不显示
        private int _hpPrivate = 50;

        // 实验4：私有【字段】+ [SerializeField] 标签 → 显示、能改、能存
        [SerializeField] private int hpSerialized = 200;

        // 顺手认识另两个标签：Header 分组标题、Range 滑块（都只是"标签"，改 Inspector 长相用的）
        [Header("这是属性测试区")]
        [Range(1, 20)] public int power = 5;

        // 实验5：Dictionary（字典）默认不被序列化，Inspector 里看不到，也存不下来
        public Dictionary<string, int> bag = new Dictionary<string, int>();
    }
}