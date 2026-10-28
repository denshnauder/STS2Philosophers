extends SceneTree

func add_file_or_fail(packer: PCKPacker, virtual_path: String, source_path: String) -> int:
	var error := packer.add_file(virtual_path, source_path)
	if error != OK:
		push_error("Could not add %s from %s: %s" % [virtual_path, source_path, error_string(error)])
	return error


func add_portrait_alias(packer: PCKPacker, alias_path: String, source_path: String) -> int:
	var metadata := ConfigFile.new()
	var error := metadata.load(source_path + ".import")
	if error != OK:
		return error
	# Reuse the imported texture, while keeping each public ModelId path valid.
	# The cache filename comes from Godot's current import, never a copied hash.
	metadata.erase_section_key("remap", "uid")
	metadata.set_value("deps", "source_file", alias_path)
	var directory := ProjectSettings.globalize_path("res://.godot/portrait_aliases")
	error = DirAccess.make_dir_recursive_absolute(directory)
	if error != OK:
		return error
	var metadata_path := directory.path_join(alias_path.get_file() + ".import")
	error = metadata.save(metadata_path)
	if error != OK:
		return error
	error = add_file_or_fail(packer, alias_path, ProjectSettings.globalize_path(source_path))
	if error != OK:
		return error
	return add_file_or_fail(packer, alias_path + ".import", metadata_path)


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

	# The Protagoras event deliberately reuses the existing Philosophers' Gaze
	# background. Pack it under the event ModelId path without duplicating the
	# binary source or imported texture.
	for alias in [
		["res://images/events/democritus_arrangement.png", "res://images/events/philosophers_gaze.png"],
		["res://images/events/democritus_arrangement.png.import", "res://images/events/democritus_arrangement.png.import"],
		["res://images/events/aristotle_civic_inquiry.png", "res://images/events/philosophers_gaze.png"],
		["res://images/events/aristotle_civic_inquiry.png.import", "res://images/events/aristotle_civic_inquiry.png.import"],
		["res://images/events/parmenides_inquiry.png", "res://images/events/philosophers_gaze.png"],
		["res://images/events/parmenides_inquiry.png.import", "res://images/events/parmenides_inquiry.png.import"],
		["res://images/events/anaximander_origin_inquiry.png", "res://images/events/philosophers_gaze.png"],
		["res://images/events/anaximander_origin_inquiry.png.import", "res://images/events/anaximander_origin_inquiry.png.import"],
		["res://images/events/empedocles_roots_inquiry.png", "res://images/events/philosophers_gaze.png"],
		["res://images/events/empedocles_roots_inquiry.png.import", "res://images/events/empedocles_roots_inquiry.png.import"],
		["res://images/events/anaxagoras_mixture_inquiry.png", "res://images/events/philosophers_gaze.png"],
		["res://images/events/anaxagoras_mixture_inquiry.png.import", "res://images/events/anaxagoras_mixture_inquiry.png.import"],
		["res://images/events/gorgias_speech_inquiry.png", "res://images/events/philosophers_gaze.png"],
		["res://images/events/gorgias_speech_inquiry.png.import", "res://images/events/gorgias_speech_inquiry.png.import"],
		["res://images/events/seneca_daily_review.png", "res://images/events/philosophers_gaze.png"],
		["res://images/events/seneca_daily_review.png.import", "res://images/events/seneca_daily_review.png.import"],
		["res://images/events/epictetus_guide_practice.png", "res://images/events/philosophers_gaze.png"],
		["res://images/events/epictetus_guide_practice.png.import", "res://images/events/epictetus_guide_practice.png.import"],
		["res://images/events/marcus_aurelius_camp_practice.png", "res://images/events/philosophers_gaze.png"],
		["res://images/events/marcus_aurelius_camp_practice.png.import", "res://images/events/marcus_aurelius_camp_practice.png.import"],
		["res://images/events/pyrrho_reported_crossing.png", "res://images/events/philosophers_gaze.png"],
		["res://images/events/pyrrho_reported_crossing.png.import", "res://images/events/pyrrho_reported_crossing.png.import"],
		[
			"res://images/events/descartes_methodic_reconstruction.png",
			"res://images/events/philosophers_gaze.png",
		],
		[
			"res://images/events/descartes_methodic_reconstruction.png.import",
			"res://images/events/descartes_methodic_reconstruction.png.import",
		],
		[
			"res://images/events/sextus_opposed_appearances.png",
			"res://images/events/philosophers_gaze.png",
		],
		[
			"res://images/events/sextus_opposed_appearances.png.import",
			"res://images/events/sextus_opposed_appearances.png.import",
		],
		[
			"res://images/events/epicurus_shared_margin.png",
			"res://images/events/philosophers_gaze.png",
		],
		[
			"res://images/events/epicurus_shared_margin.png.import",
			"res://images/events/epicurus_shared_margin.png.import",
		],
		[
			"res://images/events/han_fei_public_standard.png",
			"res://images/events/philosophers_gaze.png",
		],
		[
			"res://images/events/han_fei_public_standard.png.import",
			"res://images/events/han_fei_public_standard.png.import",
		],
		[
			"res://images/events/plato_whole_arrangement.png",
			"res://images/events/philosophers_gaze.png",
		],
		[
			"res://images/events/plato_whole_arrangement.png.import",
			"res://images/events/plato_whole_arrangement.png.import",
		],
		[
			"res://images/events/protagoras_shared_deliberation.png",
			"res://images/events/philosophers_gaze.png",
		],
		[
			"res://images/events/protagoras_shared_deliberation.png.import",
			"res://images/events/protagoras_shared_deliberation.png.import",
		],
	]:
		error = add_file_or_fail(
			packer,
			alias[0],
			ProjectSettings.globalize_path(alias[1])
		)
		if error != OK:
			quit(error)
			return

	# These events use the same existing background as the other route events.
	# Omitting their default native portrait path makes EventRoom fail to open.
	for name in [
		"anaximenes_breath_invitation", "anselm_unfinished_proof", "aquinas_bridge_notice",
		"augustine_promise", "diogenes_stay_switch", "luther_signed_care",
		"bacon_sample_ledger", "copernicus_shifted_center", "galileo_measured_practice",
		"machiavelli_flood_preparation", "newton_two_sites", "spinoza_cause_network", "ockham_names_inquiry",
		"plotinus_common_ground", "zeno_civic_challenge", "leibniz_independent_clocks",
		"machiavelli_realist_challenge", "hobbes_authorized_sovereignty", "locke_cargo_identity", "hume_induction", "wollstonecraft_admission", "kant_apperception", "rousseau_consent", "kant_universalization", "mill_harm", "beauvoir_other",
	]:
		error = add_portrait_alias(packer, "res://images/events/" + name + ".png",
			"res://images/events/philosophers_gaze.png")
		if error != OK:
			push_error("Could not pack event portrait %s: %s" % [name, error_string(error)])
			quit(error)
			return

	error = packer.flush()
	if error != OK:
		push_error("Could not finish content PCK: %s" % error_string(error))
		quit(error)
		return

	quit(0)
