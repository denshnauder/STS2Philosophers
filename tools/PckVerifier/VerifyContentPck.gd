extends SceneTree

const ADDITIONAL_EVENT_TEXTURES := [
	"res://images/events/anaximenes_breath_invitation.png",
	"res://images/events/anselm_unfinished_proof.png",
	"res://images/events/aquinas_bridge_notice.png",
	"res://images/events/augustine_promise.png",
	"res://images/events/bacon_sample_ledger.png",
	"res://images/events/copernicus_shifted_center.png",
	"res://images/events/galileo_measured_practice.png",
	"res://images/events/diogenes_stay_switch.png",
	"res://images/events/luther_signed_care.png",
	"res://images/events/machiavelli_flood_preparation.png",
	"res://images/events/newton_two_sites.png",
	"res://images/events/spinoza_cause_network.png",
	"res://images/events/ockham_names_inquiry.png",
	"res://images/events/plotinus_common_ground.png",
	"res://images/events/zeno_civic_challenge.png",
	"res://images/events/leibniz_independent_clocks.png",
	"res://images/events/machiavelli_realist_challenge.png",
	"res://images/events/hobbes_authorized_sovereignty.png",
	"res://images/events/locke_cargo_identity.png",
	"res://images/events/hume_induction.png",
	"res://images/events/wollstonecraft_admission.png",
]

const REQUIRED_FILES := [
	"res://images/events/democritus_arrangement.png",
	"res://images/events/empedocles_roots_inquiry.png",
	"res://images/events/anaxagoras_mixture_inquiry.png",
	"res://images/events/gorgias_speech_inquiry.png",
	"res://images/events/seneca_daily_review.png",
	"res://images/events/epictetus_guide_practice.png",
	"res://images/events/marcus_aurelius_camp_practice.png",
	"res://images/events/pyrrho_reported_crossing.png",
	"res://images/events/parmenides_inquiry.png",
	"res://images/events/aristotle_civic_inquiry.png",
	"res://STS2Philosophers/localization/eng/events.json",
	"res://STS2Philosophers/localization/eng/relics.json",
	"res://STS2Philosophers/localization/zhs/events.json",
	"res://STS2Philosophers/localization/zhs/relics.json",
	"res://images/events/philosophers_gaze.png",
	"res://images/events/descartes_methodic_reconstruction.png",
	"res://images/events/sextus_opposed_appearances.png",
	"res://images/events/epicurus_shared_margin.png",
	"res://images/events/plato_whole_arrangement.png",
	"res://images/events/protagoras_shared_deliberation.png",
	"res://images/events/zeno_assent_boundary.png",
	"res://STS2Philosophers/images/kongzi_muduo.png",
	"res://STS2Philosophers/images/kongzi_muduo_outline.png",
	"res://STS2Philosophers/images/kongzi_qing_yu_pei.png",
	"res://STS2Philosophers/images/kongzi_qing_yu_pei_outline.png",
	"res://STS2Philosophers/images/mengzi_xiong_zhang.png",
	"res://STS2Philosophers/images/mengzi_xiong_zhang_outline.png",
	"res://STS2Philosophers/images/xunzi_sheng_mo.png",
	"res://STS2Philosophers/images/xunzi_sheng_mo_outline.png",
	"res://STS2Philosophers/images/mozi_mo_se_zhu_jian.png",
	"res://STS2Philosophers/images/mozi_mo_se_zhu_jian_outline.png",
	"res://STS2Philosophers/images/mozi_shou_cheng_tu.png",
	"res://STS2Philosophers/images/mozi_shou_cheng_tu_outline.png",
	"res://STS2Philosophers/images/laozi_wu_wei_shu_jian.png",
	"res://STS2Philosophers/images/laozi_wu_wei_shu_jian_outline.png",
	"res://STS2Philosophers/images/laozi_shui_yu.png",
	"res://STS2Philosophers/images/laozi_shui_yu_outline.png",
	"res://STS2Philosophers/images/qin_guli_shou_cheng_xie.png",
	"res://STS2Philosophers/images/qin_guli_shou_cheng_xie_outline.png",
	"res://STS2Philosophers/images/zhuangzi_da_hu.png",
	"res://STS2Philosophers/images/zhuangzi_da_hu_outline.png",
	"res://STS2Philosophers/images/yangzhu_quan_sheng_bi.png",
	"res://STS2Philosophers/images/yangzhu_quan_sheng_bi_outline.png",
	"res://STS2Philosophers/images/huishi_li_wu_chou.png",
	"res://STS2Philosophers/images/huishi_li_wu_chou_outline.png",
]

const REQUIRED_TEXTURES := [
	"res://images/events/democritus_arrangement.png",
	"res://images/events/empedocles_roots_inquiry.png",
	"res://images/events/anaxagoras_mixture_inquiry.png",
	"res://images/events/gorgias_speech_inquiry.png",
	"res://images/events/seneca_daily_review.png",
	"res://images/events/epictetus_guide_practice.png",
	"res://images/events/marcus_aurelius_camp_practice.png",
	"res://images/events/pyrrho_reported_crossing.png",
	"res://images/events/parmenides_inquiry.png",
	"res://images/events/aristotle_civic_inquiry.png",
	"res://STS2Philosophers/images/heraclitus_lyre.svg",
	"res://STS2Philosophers/images/heraclitus_lyre_outline.svg",
	"res://STS2Philosophers/images/socrates_question_cup.svg",
	"res://STS2Philosophers/images/socrates_question_cup_outline.svg",
	"res://STS2Philosophers/images/socrates_balance_weight.svg",
	"res://STS2Philosophers/images/socrates_balance_weight_outline.svg",
	"res://STS2Philosophers/images/plato_civic_seal.svg",
	"res://STS2Philosophers/images/plato_civic_seal_outline.svg",
	"res://STS2Philosophers/images/descartes_thinking_lens.svg",
	"res://STS2Philosophers/images/descartes_thinking_lens_outline.svg",
	"res://STS2Philosophers/images/aristotle_category_tablet.svg",
	"res://STS2Philosophers/images/aristotle_category_tablet_outline.svg",
	"res://STS2Philosophers/images/rousseau_uncarved_stone.svg",
	"res://STS2Philosophers/images/rousseau_uncarved_stone_outline.svg",
	"res://images/events/philosophers_gaze.png",
	"res://images/events/descartes_methodic_reconstruction.png",
	"res://images/events/sextus_opposed_appearances.png",
	"res://images/events/epicurus_shared_margin.png",
	"res://images/events/plato_whole_arrangement.png",
	"res://images/events/protagoras_shared_deliberation.png",
	"res://images/events/zeno_assent_boundary.png",
	"res://STS2Philosophers/images/kongzi_muduo.png",
	"res://STS2Philosophers/images/kongzi_muduo_outline.png",
	"res://STS2Philosophers/images/kongzi_qing_yu_pei.png",
	"res://STS2Philosophers/images/kongzi_qing_yu_pei_outline.png",
	"res://STS2Philosophers/images/mengzi_xiong_zhang.png",
	"res://STS2Philosophers/images/mengzi_xiong_zhang_outline.png",
	"res://STS2Philosophers/images/xunzi_sheng_mo.png",
	"res://STS2Philosophers/images/xunzi_sheng_mo_outline.png",
	"res://STS2Philosophers/images/mozi_mo_se_zhu_jian.png",
	"res://STS2Philosophers/images/mozi_mo_se_zhu_jian_outline.png",
	"res://STS2Philosophers/images/mozi_shou_cheng_tu.png",
	"res://STS2Philosophers/images/mozi_shou_cheng_tu_outline.png",
	"res://STS2Philosophers/images/laozi_wu_wei_shu_jian.png",
	"res://STS2Philosophers/images/laozi_wu_wei_shu_jian_outline.png",
	"res://STS2Philosophers/images/laozi_shui_yu.png",
	"res://STS2Philosophers/images/laozi_shui_yu_outline.png",
	"res://STS2Philosophers/images/qin_guli_shou_cheng_xie.png",
	"res://STS2Philosophers/images/qin_guli_shou_cheng_xie_outline.png",
	"res://STS2Philosophers/images/zhuangzi_da_hu.png",
	"res://STS2Philosophers/images/zhuangzi_da_hu_outline.png",
	"res://STS2Philosophers/images/yangzhu_quan_sheng_bi.png",
	"res://STS2Philosophers/images/yangzhu_quan_sheng_bi_outline.png",
	"res://STS2Philosophers/images/huishi_li_wu_chou.png",
	"res://STS2Philosophers/images/huishi_li_wu_chou_outline.png",
]

func _initialize() -> void:
	var args := OS.get_cmdline_user_args()
	if args.size() != 1:
		push_error("Expected exactly one PCK path.")
		quit(2)
		return

	if not ProjectSettings.load_resource_pack(args[0], true):
		push_error("Could not load content PCK: %s" % args[0])
		quit(3)
		return

	for path in REQUIRED_FILES + ADDITIONAL_EVENT_TEXTURES:
		if not FileAccess.file_exists(path):
			push_error("Missing packed file: %s" % path)
			quit(4)
			return

	for path in REQUIRED_TEXTURES + ADDITIONAL_EVENT_TEXTURES:
		var texture := ResourceLoader.load(path, "Texture2D", ResourceLoader.CACHE_MODE_IGNORE)
		if texture == null:
			push_error("Packed texture is not loadable: %s" % path)
			quit(5)
			return

	print("Content PCK checks passed.")
	quit(0)
