using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// 武器状态（拔刀 / 收刀）—— 负责**换整套形象**，不负责状态机。
    ///
    /// <para>⭐ 设计要点：拔刀/收刀**不进 <see cref="PlayerFSM"/>**。它只切三块渲染器的显隐，
    /// 不阻塞移动、跳跃、翻滚，所以不配当一个"状态"。上半身的拔刀动作由 Animator 的
    /// UpperBody 层（Avatar Mask）叠加播放，与这里解耦。</para>
    ///
    /// <para><b>两套形象（2026-10-20 定稿，用的是 Player3 预制体）</b>：</para>
    /// <list type="bullet">
    ///   <item><b>拔刀态</b> = `Equips`（Player2 原版护甲，无背剑）+ `Roskva_Sword_Hand`（手剑，717 顶点绑 `Bip001-R-Hand.002`）。</item>
    ///   <item><b>收刀态</b> = `Equips_Sheathed`（把 Player1 的 `Equips` 整份复制过来、含背剑，
    ///         27 根骨骼按名字重映射到 P2 骨架）+ 隐藏手剑。`Equips` 同时关闭。</item>
    /// </list>
    ///
    /// <para>⚠️ 两套护甲是**互斥**的，必须一开一关：它们的几何几乎完全重叠（都是 Roskva 的护甲），
    /// 同时开会 Z-fighting 闪烁。别为了"省一次开关"让它们共存。</para>
    ///
    /// <para>⚠️ 动画资源还没到位：`Draw` / `Sheath` 两个上半身动作、以及独立的走路/跑步动画
    /// 都还没做。现在 <see cref="SetArmed"/> 是**瞬时切换**——等动画接进来后，
    /// 把显隐时机挪到"手触到剑柄那一刻"（动画事件或 StateMachineBehaviour 按 normalizedTime 触发），
    /// 否则会看到剑瞬移。</para>
    /// </summary>
    public class PlayerEquip : MonoBehaviour
    {
        [Header("拔刀态（手上握剑）")]
        [Tooltip("Player2 原版护甲渲染器（无背剑）。拔刀时启用。")]
        [SerializeField] private SkinnedMeshRenderer _armedEquips;

        [Tooltip("手上的剑（Roskva_Sword_Hand）。拔刀时启用。")]
        [SerializeField] private SkinnedMeshRenderer _handSword;

        [Header("收刀态（剑背在背上）")]
        [Tooltip("Player1 护甲的副本（含背剑，网格 Roskva_Equips_Sheathed）。收刀时启用。")]
        [SerializeField] private SkinnedMeshRenderer _sheathedEquips;

        [Header("动画（资源到位后接）")]
        [Tooltip("Animator。留空则自动取本物体上的。")]
        [SerializeField] private Animator _anim;

        [Tooltip("Animator 里表示\"已拔刀\"的 Bool 参数名。留空 = 不写（controller 里还没这个参数时不报错）。")]
        [SerializeField] private string _armedParam = "WeaponDrawn";

        [Tooltip("拔刀动作的 Trigger 名（UpperBody 层用）。留空 = 不触发。")]
        [SerializeField] private string _drawTrigger = "";

        [Tooltip("收刀动作的 Trigger 名。留空 = 不触发。")]
        [SerializeField] private string _sheathTrigger = "";

        [Tooltip("初始是否已拔刀。⚠️ 编辑器里改这个值会立即预览形象（非运行状态）。")]
        [SerializeField] private bool _startArmed = true;

        [Header("调试")]
        [Tooltip("临时调试键：按一下切换拔刀/收刀。正式接输入后设成 None 关掉。")]
        [SerializeField] private KeyCode _debugToggleKey = KeyCode.T;

        /// <summary>当前是否已拔刀。⚠️ 这是**逻辑与形象**的唯一真相源。</summary>
        public bool IsArmed { get; private set; }

        /// <summary>正在播放拔刀/收刀动作（这段时间内不应接受第二次切换）。</summary>
        public bool IsSwitching { get; private set; }

        private void Awake()
        {
            if (_anim == null) _anim = GetComponent<Animator>();
        }

        /// <summary>
        /// Inspector 里改 <see cref="_startArmed"/> 时**立刻**在编辑器中预览形象，
        /// 不用进 Play。只在非运行状态生效，避免打乱运行时的 `IsArmed`。
        /// </summary>
        private void OnValidate()
        {
            if (Application.isPlaying) return;
            PreviewInEditor(_startArmed);
        }

        /// <summary>编辑器预览：只改三个渲染器的开关，不动 Animator、不改 IsArmed。</summary>
        private void PreviewInEditor(bool armed)
        {
            if (_armedEquips != null) _armedEquips.enabled = armed;
            if (_sheathedEquips != null) _sheathedEquips.enabled = !armed;
            if (_handSword != null) _handSword.enabled = armed;
        }

        /// <summary>Inspector 组件右上角 ⋮ 菜单 → 切到拔刀态（编辑器内直接预览）。</summary>
        [ContextMenu("切到拔刀态（手上握剑）")]
        private void ContextArmed()
        {
            _startArmed = true;
            if (Application.isPlaying) SetArmed(true, false);
            else PreviewInEditor(true);
        }

        /// <summary>Inspector 组件右上角 ⋮ 菜单 → 切到收刀态（剑背在背上）。</summary>
        [ContextMenu("切到收刀态（剑在背上）")]
        private void ContextSheathed()
        {
            _startArmed = false;
            if (Application.isPlaying) SetArmed(false, false);
            else PreviewInEditor(false);
        }

        private void Start()
        {
            //⚠️ 不用 Awake：引用的可能是别的物体上的渲染器，Start 阶段更稳。
            Apply(_startArmed, false);
        }

        /// <summary>
        /// 临时调试键。
        /// ⚠️ 项目 `activeInputHandler = 2`（Both），所以旧版 <c>Input</c> API 可用；
        ///   正式接入输入系统后把 <see cref="_debugToggleKey"/> 设成 <c>None</c>。
        /// </summary>
        private void Update()
        {
            if (_debugToggleKey == KeyCode.None) return;
            if (Input.GetKeyDown(_debugToggleKey)) Toggle();
        }

        /// <summary>切换拔刀 / 收刀。挂到输入上（建议 Tab）。</summary>
        public void Toggle()
        {
            if (IsSwitching) return;
            SetArmed(!IsArmed);
        }

        /// <summary>显式设置武器状态。</summary>
        /// <param name="armed">true = 拔刀（手剑 + P2 护甲）／false = 收刀（P1 护甲含背剑）。</param>
        /// <param name="playAnim">是否触发 Animator 上的动作。初始化时传 false。</param>
        public void SetArmed(bool armed, bool playAnim = true)
        {
            if (armed == IsArmed && playAnim) return;
            Apply(armed, playAnim);
        }

        private void Apply(bool armed, bool playAnim)
        {
            IsArmed = armed;

            //—— 形象：两套护甲互斥，手剑只在拔刀时出现 ——
            if (_armedEquips != null) _armedEquips.enabled = armed;
            if (_sheathedEquips != null) _sheathedEquips.enabled = !armed;
            if (_handSword != null) _handSword.enabled = armed;

            //—— 动画：controller 里没有对应参数时静默跳过 ——
            if (_anim != null)
            {
                if (!string.IsNullOrEmpty(_armedParam) && HasParameter(_armedParam))
                    _anim.SetBool(_armedParam, armed);

                if (playAnim)
                {
                    string trig = armed ? _drawTrigger : _sheathTrigger;
                    if (!string.IsNullOrEmpty(trig) && HasParameter(trig))
                        _anim.SetTrigger(trig);
                }
            }
        }

        /// <summary>
        /// 查 Animator 有没有这个参数。
        /// ⚠️ 直接 <c>SetBool</c> 一个不存在的参数，Unity 会**每帧刷一条警告**，
        ///   在动画资源还没接进来之前会污染 Console。
        /// </summary>
        private bool HasParameter(string name)
        {
            var ps = _anim.parameters;
            for (int i = 0; i < ps.Length; i++)
                if (ps[i].name == name) return true;
            return false;
        }

        /// <summary>
        /// 拔刀动作播完时由动画事件/StateMachineBehaviour 调用，
        /// 解除 <see cref="IsSwitching"/> 锁。资源到位前不会有人调，留着接口。
        /// </summary>
        public void OnSwitchAnimFinished()
        {
            IsSwitching = false;
        }
    }
}
