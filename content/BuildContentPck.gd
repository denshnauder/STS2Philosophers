extends SceneTree

func add_file_or_fail(packer: PCKPacker, virtual_path: String, source_path: String) -> int:
	var error := packer.add_file(virtual_path, source_path)
	if error != OK:
		push_error("Could not add %s from %s: %s" % [virtual_path, source_path, error_string(error)])
	return error


func _initialize() -> void:
	var args := OS.get_cmdline_user_args()
	if args.size() != 1:
		push_error("Expected exactly one output path for the content PCK.")
		quit(2)
		return

	var packer := PCKPacker.new()
	var error := packer.pck_start(args[0])
	if error != OK:
		push_error("Could not create content PCK: %s" % error_string(error))
		quit(error)
		return

	var resource_paths: Array[String] = [
		"STS2Philosophers/localization/eng/events.json",
		"STS2Philosophers/localization/eng/relics.json",
		"STS2Philosophers/localization/zhs/events.json",
		"STS2Philosophers/localization/zhs/relics.json",
	]

	for resource_path in resource_paths:
		error = add_file_or_fail(
			packer,
			"res://" + resource_path,
			ProjectSettings.globalize_path("res://" + resource_path)
		)
		if error != OK:
			quit(error)
			return

	var texture_paths: Array[String] = [
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
		"res://STS2Philosophers/images/huishi_li_wu_chou.png",
		"res://STS2Philosophers/images/huishi_li_wu_chou_outline.png",
	]

	for texture_path in texture_paths:
		var import_path: String = texture_path + ".import"
		var import_config := ConfigFile.new()
		error = import_config.load(import_path)
		if error != OK:
			push_error("Could not load texture import metadata %s: %s" % [import_path, error_string(error)])
			quit(error)
			return

		# Godot owns imported cache filenames, including its required source-hash hyphen.
		var imported_texture_path: String = import_config.get_value("remap", "path", "")
		if imported_texture_path.is_empty():
			push_error("Texture import metadata has no remap path: %s" % import_path)
			quit(3)
			return

		for packed_path in [texture_path, import_path, imported_texture_path]:
			error = add_file_or_fail(
				packer,
				packed_path,
				ProjectSettings.globalize_path(packed_path)
			)
			if error != OK:
				quit(error)
				return

	error = packer.flush()
	if error != OK:
		push_error("Could not finish content PCK: %s" % error_string(error))
		quit(error)
		return

	quit(0)
