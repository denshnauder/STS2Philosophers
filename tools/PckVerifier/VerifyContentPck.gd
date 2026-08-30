extends SceneTree

const REQUIRED_FILES := [
	"res://STS2Philosophers/localization/eng/events.json",
	"res://STS2Philosophers/localization/eng/relics.json",
	"res://STS2Philosophers/localization/zhs/events.json",
	"res://STS2Philosophers/localization/zhs/relics.json",
	"res://images/events/philosophers_gaze.png",
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
]

const REQUIRED_TEXTURES := [
	"res://images/events/philosophers_gaze.png",
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

	for path in REQUIRED_FILES:
		if not FileAccess.file_exists(path):
			push_error("Missing packed file: %s" % path)
			quit(4)
			return

	for path in REQUIRED_TEXTURES:
		var texture := ResourceLoader.load(path, "Texture2D", ResourceLoader.CACHE_MODE_IGNORE)
		if texture == null:
			push_error("Packed texture is not loadable: %s" % path)
			quit(5)
			return

	print("Content PCK checks passed.")
	quit(0)
