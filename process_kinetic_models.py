#!/usr/bin/env python3
"""
Process Create mod JAR files to add 'kinetic_' prefix to:
1. Fully rotating model files (rename file)
2. Rotating element/group names in partially rotating models
"""

import zipfile
import json
import os
import shutil
from pathlib import Path

# ============================================================
# CONFIGURATION: Define which models are kinetic (fully rotating)
# ============================================================

# Fully rotating model files (these get renamed with kinetic_ prefix)
# Format: "path/to/model.json" (relative to assets/create/models/block/)
KINETIC_MODEL_FILES = {
    # Top-level cogwheels
    "cogwheel.json",
    "large_cogwheel.json",
    "large_cogwheel_shaftless.json",
    "cogwheel_shaftless.json",
    "turntable.json",
    "belt_pulley.json",
    "clockwork_bearing.json",
    "mechanical_bearing.json",
    "windmill_bearing.json",
    "powered_shaft.json",
    # Encased cogwheels (rotating parts)
    "andesite_encased_cogwheel.json",
    "andesite_encased_cogwheel_top.json",
    "andesite_encased_cogwheel_bottom.json",
    "andesite_encased_cogwheel_top_bottom.json",
    "andesite_encased_large_cogwheel.json",
    "andesite_encased_large_cogwheel_top.json",
    "andesite_encased_large_cogwheel_bottom.json",
    "andesite_encased_large_cogwheel_top_bottom.json",
    "brass_encased_cogwheel.json",
    "brass_encased_cogwheel_top.json",
    "brass_encased_cogwheel_bottom.json",
    "brass_encased_cogwheel_top_bottom.json",
    "brass_encased_large_cogwheel.json",
    "brass_encased_large_cogwheel_top.json",
    "brass_encased_large_cogwheel_bottom.json",
    "brass_encased_large_cogwheel_top_bottom.json",
    # Water wheels (rotating wheel part)
    "water_wheel/wheel.json",
    "large_water_wheel/block_extension.json",
    # Encased fan (propeller rotates)
    "encased_fan/propeller.json",
    # Mechanical drill (head rotates)
    "mechanical_drill/head.json",
    # Mechanical arm (all arm parts rotate)
    "mechanical_arm/upper_body.json",
    "mechanical_arm/lower_body.json",
    "mechanical_arm/upper_claw_grip.json",
    "mechanical_arm/lower_claw_grip.json",
    "mechanical_arm/claw_base.json",
    "mechanical_arm/base.json",
    "mechanical_arm/cog.json",
    # Flywheel
    "flywheel/flywheel.json",
    # Millstone (inner rotates)
    "millstone/inner.json",
    # Mechanical roller (wheel rotates)
    "mechanical_roller/wheel.json",
    # Chain conveyor (wheel and shaft rotate)
    "chain_conveyor/wheel.json",
    "chain_conveyor/shaft.json",
    # Steam engine (piston, linkage, shaft_connector, gauge_dial move/rotate)
    "steam_engine/piston.json",
    "steam_engine/linkage.json",
    "steam_engine/shaft_connector.json",
    "steam_engine/gauge_dial.json",
    # Mechanical crafter (belt, lid, arrow move)
    "mechanical_crafter/belt.json",
    "mechanical_crafter/belt_animated.json",
    "mechanical_crafter/lid.json",
    "mechanical_crafter/arrow.json",
    # Mechanical harvester (blade rotates)
    "mechanical_harvester/blade.json",
    # Mechanical pump (cog rotates)
    "mechanical_pump/cog.json",
    # Weighted ejector (top moves)
    "weighted_ejector/top.json",
    # Hand crank (handle rotates)
    "hand_crank/handle.json",
    # Bearing (top rotates)
    "bearing/top.json",
    "bearing/top_wooden.json",
    # Rope pulley (pulley_magnet, rope parts rotate)
    "rope_pulley/pulley_magnet.json",
    "rope_pulley/rope.json",
    "rope_pulley/rope_coil.json",
    "rope_pulley/rope_half.json",
    # Elevator pulley (pulley_magnet, rope parts rotate)
    "elevator_pulley/pulley_magnet.json",
    "elevator_pulley/rope.json",
    "elevator_pulley/rope_coil.json",
    "elevator_pulley/rope_half.json",
    # Hose pulley (pulley_magnet, rope, hose_coil rotate)
    "hose_pulley/pulley_magnet.json",
    "hose_pulley/rope.json",
    "hose_pulley/rope_half.json",
    "hose_pulley/rope_half_magnet.json",
    "hose_pulley/hose_coil.json",
    # Blaze burner (rods, flame, goggles rotate)
    "blaze_burner/rods_large.json",
    "blaze_burner/rods_small.json",
    "blaze_burner/flame.json",
    "blaze_burner/goggles.json",
    "blaze_burner/goggles_small.json",
    "blaze_burner/superheated_rods_large.json",
    "blaze_burner/superheated_rods_small.json",
    "blaze_burner/blaze/active.json",
    "blaze_burner/blaze/idle.json",
    "blaze_burner/blaze/inert.json",
    "blaze_burner/blaze/super.json",
    "blaze_burner/blaze/super_active.json",
    # Spout (top, middle, bottom rotate)
    "spout/top.json",
    "spout/middle.json",
    "spout/bottom.json",
    # Funnel (flap moves)
    "funnel/flap.json",
    # Encased shaft (the shaft inside rotates - but these are item models mostly)
    # Actually encased_shaft block models are stationary casings
    # Gantry shaft (rack moves linearly, not rotation)
    # Not kinetic in rotational sense
    # Deployer (pole, hand parts move)
    "deployer/pole.json",
    "deployer/hand_punching.json",
    "deployer/hand_holding.json",
    "deployer/hand_pointing.json",
    "deployer/vertical.json",
    "deployer/horizontal.json",
    # Creative motor (shaft rotates)
    "creative_motor/block.json",
    "creative_motor/block_vertical.json",
    # Cuckoo clock (moving parts)
    "cuckoo_clock/pig.json",
    "cuckoo_clock/creeper.json",
    "cuckoo_clock/door.json",
    "cuckoo_clock/moving_clock.json",  # if exists
    # Mechanical press (shaft rotates - item model only?)
    # mechanical_press is not in block models
    # Gauge (shaft rotates)
    "gauge/stressometer/block.json",  # if exists
    "gauge/speedometer/block.json",  # if exists
    # Encased chain drive (shaft rotates)
    "encased_chain_drive/block.json",  # if exists
    # Mechanical mixer (cogwheel rotates)
    "mechanical_mixer/block.json",  # if exists
    # Display board (cogwheel rotates)
    "display_board/block.json",  # if exists
    # Portable storage/fluid interface (middle rotates?)
    # These seem to be animated but not rotational kinetic
    # Belt (diagonal parts - not sure if kinetic)
    # belt_tunnel (cover - stationary)
    # Schematicannon (pipe, connector - stationary)
    # Metal girder (stationary)
    # Fluid valve (pointer rotates)
    "fluid_valve/pointer.json",
    # Analog lever (lever, handle, indicator rotate)
    "analog_lever/handle.json",
    "analog_lever/indicator.json",
    # Mechanical piston (moving parts)
    "mechanical_piston/moving/block.json",
    "mechanical_piston/moving/block_rotated.json",
    "mechanical_piston/normal/head.json",
    "mechanical_piston/sticky/head.json",
    "mechanical_piston/extended/block.json",
    "mechanical_piston/extended/block_rotated.json",
    # Sticker (head moves)
    "sticker/head.json",
    "sticker/block_powered.json",
    # Desk bell (plunger, bell move)
    "desk_bell/plunger.json",
    "desk_bell/bell.json",
    "desk_bell/block_powered.json",
    # Nozzle (item collector moves)
    "nozzle/block.json",
    # Track signal (tubes - stationary, indicator moves)
    "track_signal/indicator_on.json",
    "track_signal/indicator_off.json",
    # Rope pulley block (stationary)
    # hose_pulley block (stationary)
    # elevator_pulley block (stationary)
    # Encased cogwheel block variants (stationary casings)
    # NOT: encased_cogwheel/block.json, block_top.json, etc.
    # Gearbox (stationary housing)
    # NOT: gearbox/block.json
    # Millstone block (stationary)
    # NOT: millstone/block.json
    # Mechanical roller block (stationary frame)
    # NOT: mechanical_roller/block.json, frame.json
    # Chain conveyor block (stationary top/bottom)
    # NOT: chain_conveyor/block.json
    # Steam engine block (stationary housing)
    # NOT: steam_engine/block.json, gauge.json
    # Mechanical crafter block (stationary)
    # NOT: mechanical_crafter/block.json
    # Mechanical harvester block (stationary)
    # NOT: mechanical_harvester/block.json
    # Mechanical pump block (stationary)
    # NOT: mechanical_pump/block.json
    # Weighted ejector block (stationary)
    # NOT: weighted_ejector/block.json
    # Hand crank block (stationary base)
    # NOT: hand_crank/block.json
    # Bearing block (stationary base)
    # NOT: bearing/block.json
    # Encased fan block (stationary casing)
    # NOT: encased_fan/block.json
}

# Models that have BOTH stationary and rotating parts in the SAME file
# These need element/group names prefixed with "kinetic_"
PARTIALLY_ROTATING_MODELS = {
    # Format: "path/to/model.json": {
    #     "groups": ["group_name1", "group_name2"],  # group names to prefix
    #     "elements": ["element_name1", "element_name2"]  # element names to prefix
    # }
    # Cuckoo clock has moving parts (Door, MovingClock, CuckooClock)
    "cuckoo_clock/block.json": {
        "groups": ["Door", "MovingClock", "CuckooClock"],
        "elements": [],
    },
    "cuckoo_clock/item.json": {
        "groups": ["Door", "MovingClock", "CuckooClock"],
        "elements": [],
    },
    # Netherite backtank (Axle rotates, Gears rotate)
    "netherite_backtank/block.json": {"groups": ["Axle"], "elements": []},
    "netherite_backtank/block_cogs.json": {"groups": ["Gears", "Axle"], "elements": []},
    "netherite_backtank/item.json": {"groups": ["Gears", "Axle"], "elements": []},
    # Copper backtank (same)
    "copper_backtank/block.json": {"groups": ["Axle"], "elements": []},
    "copper_backtank/block_cogs.json": {"groups": ["Gears", "Axle"], "elements": []},
    "copper_backtank/item.json": {"groups": ["Gears", "Axle"], "elements": []},
    # Creative motor (shaft rotates)
    "creative_motor/block.json": {"groups": ["shaft"], "elements": []},
    "creative_motor/block_vertical.json": {"groups": ["shaft"], "elements": []},
    "creative_motor/item.json": {"groups": ["shaft"], "elements": []},
    # Deployer (pole, hand parts move)
    "deployer/item.json": {"groups": ["pole", "hand_pointing"], "elements": []},
    # Mechanical arm item (Arm 1 rotates)
    "mechanical_arm/item.json": {"groups": ["Arm 1"], "elements": []},
    # Encased fan item (inner rotates)
    "encased_fan/item.json": {"groups": ["encased_fan_inner"], "elements": []},
    # Encased cogwheel item (cogwheel rotates)
    "encased_cogwheel/item.json": {"groups": ["cogwheel"], "elements": []},
    # Encased large cogwheel item (large_cogwheel rotates)
    "encased_large_cogwheel/item.json": {"groups": ["large_cogwheel"], "elements": []},
    # Mechanical crafter item (lid, cogwheel_shaftless rotate)
    "mechanical_crafter/item.json": {
        "groups": ["lid", "cogwheel_shaftless"],
        "elements": [],
    },
    # Steam engine item (Piston, Linkage rotate)
    "steam_engine/item.json": {"groups": ["Piston", "Linkage"], "elements": []},
    # Weighted ejector item (Panel, Axle rotate)
    "weighted_ejector/item.json": {"groups": ["Panel", "Axle"], "elements": []},
    # Hand crank item (shaft_half rotates)
    "hand_crank/item.json": {"groups": ["shaft_half"], "elements": []},
    # Mechanical pump cog (cogwheel rotates)
    "mechanical_pump/cog.json": {"groups": ["cogwheel"], "elements": []},
    # Mechanical arm cog (inserter rotates)
    "mechanical_arm/cog.json": {"groups": ["inserter"], "elements": []},
    # Hose pulley item (hose coil, shaft rotate)
    "hose_pulley/item.json": {"groups": ["hose coil", "shaft"], "elements": []},
    # Rope pulley item (shaft, rope_coil, rope_half_magnet rotate)
    "rope_pulley/item.json": {
        "groups": ["shaft", "rope_coil", "rope_half_magnet"],
        "elements": [],
    },
    # Elevator pulley item (Frame, Anchor, shaft, rope_coil - shaft and rope_coil rotate)
    "elevator_pulley/item.json": {"groups": ["shaft", "rope_coil"], "elements": []},
    # Gearbox item (shafts rotate)
    "gearbox/item.json": {"groups": ["shaft"], "elements": []},
    "gearbox/item_vertical.json": {"groups": ["shaft"], "elements": []},
    # Encased shaft items (shaft rotates)
    "encased_shaft/item.json": {"groups": ["shaft"], "elements": []},
    "encased_shaft/item_andesite.json": {"groups": ["shaft"], "elements": []},
    "encased_shaft/item_brass.json": {"groups": ["shaft"], "elements": []},
    # Netherite/copper backtank block_shaft_input (Axle rotates)
    "netherite_backtank/block_shaft_input.json": {"groups": ["Axle"], "elements": []},
    "copper_backtank/block_shaft_input.json": {"groups": ["Axle"], "elements": []},
    # Millstone item (cogwheel rotates)
    "millstone/item.json": {"groups": ["cogwheel"], "elements": []},
    # Display board item (cogwheel_shaftless rotates)
    "display_board/item.json": {"groups": ["cogwheel_shaftless"], "elements": []},
    # Mechanical mixer item (cogwheel_shaftless rotates)
    "mechanical_mixer/item.json": {"groups": ["cogwheel_shaftless"], "elements": []},
    # Gauge items (shaft rotates)
    "gauge/stressometer/item.json": {"groups": ["shaft"], "elements": []},
    "gauge/speedometer/item.json": {"groups": ["shaft"], "elements": []},
    # Clutch item (shaft rotates)
    "clutch/item.json": {"groups": ["shaft"], "elements": []},
    # Encased chain drive item (shaft rotates)
    "encased_chain_drive/item.json": {"groups": ["shaft"], "elements": []},
    # Gantry carriage wheels (cogwheel_shaftless rotates)
    "gantry_carriage/wheels.json": {"groups": ["cogwheel_shaftless"], "elements": []},
    "gantry_carriage/item.json": {"groups": ["cogwheel_shaftless"], "elements": []},
    # Fluid valve item (shaft, hands rotate)
    "fluid_valve/item.json": {"groups": ["shaft", "hands"], "elements": []},
    "fluid_valve/pointer.json": {"groups": ["group"], "elements": []},
    # Analog lever (lever rotates)
    "analog_lever/block.json": {"groups": ["lever"], "elements": []},
    "analog_lever/handle.json": {"groups": ["lever"], "elements": []},
    "analog_lever/indicator.json": {"groups": ["lever"], "elements": []},
    "analog_lever/item.json": {"groups": ["lever"], "elements": []},
    # Funnel item (flap moves)
    "funnel/item.json": {"groups": ["flap"], "elements": []},
    # Belt tunnel item (Flap moves)
    "belt_tunnel/item.json": {"groups": ["Flap"], "elements": []},
    # Spout item (nozzle rotates?)
    "spout/item.json": {"groups": ["nozzle"], "elements": []},
    # Track signal item (indicator_on rotates)
    "track_signal/item.json": {"groups": ["indicator_on"], "elements": []},
    # Package frogport (tongue, head move)
    "package_frogport/tongue.json": {"groups": ["port_tongue"], "elements": []},
    "package_frogport/head.json": {"groups": ["port_head"], "elements": []},
    "package_frogport/head_goggles.json": {
        "groups": ["port_head", "froggles"],
        "elements": [],
    },
    "package_frogport/body.json": {"groups": ["port_body"], "elements": []},
    "package_frogport/item.json": {
        "groups": ["port_tongue", "port_head"],
        "elements": [],
    },
    # Desk bell (plunger, bell move)
    "desk_bell/block.json": {"groups": ["plunger", "bell"], "elements": []},
    "desk_bell/block_powered.json": {"groups": ["plunger", "bell"], "elements": []},
    "desk_bell/item.json": {"groups": ["plunger", "bell"], "elements": []},
    # Sticker (head moves)
    "sticker/block.json": {
        "groups": [
            "Piston Head"
        ],  # actually Piston Block is stationary, Piston Head moves
        "elements": [],
    },
    "sticker/block_powered.json": {"groups": ["Piston Head"], "elements": []},
    "sticker/item.json": {"groups": ["Piston Head"], "elements": []},
    # Mechanical drill item (head rotates)
    "mechanical_drill/item.json": {"groups": ["head"], "elements": []},
    # Flywheel item (flywheel rotates - but it's the same model)
    # The item model for flywheel likely references the flywheel model
    # Chain conveyor item (wheel, shaft rotate)
    "chain_conveyor/item.json": {
        "groups": [],  # check if it has groups
        "elements": [],
    },
    # Elevator pulley block (Frame stationary, Anchor rotates?)
    "elevator_pulley/block.json": {
        "groups": ["Anchor"],  # Anchor might rotate
        "elements": [],
    },
    "elevator_pulley/pulley_magnet.json": {
        "groups": ["Anchor"],  # this IS the rotating part
        "elements": [],
    },
    "elevator_pulley/rope_coil.json": {
        "groups": ["Spool"],  # rotates
        "elements": [],
    },
    "elevator_pulley/rope_half.json": {
        "groups": ["rope"],  # moves
        "elements": [],
    },
    # Rope pulley block (stationary frame, pulley_magnet rotates)
    "rope_pulley/block.json": {
        "groups": [],  # check
        "elements": [],
    },
    "rope_pulley/pulley_magnet.json": {
        "groups": [],  # this IS the rotating part
        "elements": [],
    },
    "rope_pulley/rope_coil.json": {
        "groups": ["hose coil"],  # rotates
        "elements": [],
    },
    "rope_pulley/rope_half.json": {
        "groups": [],  # check
        "elements": [],
    },
    # Belt funnel (Front_section moves)
    "belt_funnel/block_pulling.json": {"groups": ["Front_section"], "elements": []},
    "belt_funnel/block_pushing.json": {"groups": ["Front_section"], "elements": []},
    "belt_funnel/block_retracted.json": {"groups": [], "elements": []},
    "belt_funnel/block_extended.json": {"groups": [], "elements": []},
    # Gantry shaft (rack moves linearly)
    "gantry_shaft/block_start.json": {"groups": ["rack"], "elements": []},
    "gantry_shaft/block_end.json": {"groups": ["rack"], "elements": []},
    "gantry_shaft/block_middle.json": {"groups": ["rack"], "elements": []},
    "gantry_shaft/block_single.json": {"groups": ["rack"], "elements": []},
    # Track station (Flag moves)
    "track_station/flag_off.json": {"groups": ["Flag"], "elements": []},
    "track_station/flag_on.json": {"groups": ["Flag"], "elements": []},
    "track_station/flag_assemble.json": {"groups": ["Flag"], "elements": []},
    "track_station/item.json": {"groups": ["Flag"], "elements": []},
    # Steam whistle (Whistle parts, rims move)
    "steam_whistle/block_small_floor.json": {
        "groups": ["Whistle Small", "small_top_rim"],
        "elements": [],
    },
    "steam_whistle/block_small_wall.json": {
        "groups": ["Whistle Small", "small_top_rim"],
        "elements": [],
    },
    "steam_whistle/block_medium_floor.json": {
        "groups": ["Whistle Medium", "medium_top_rim"],
        "elements": [],
    },
    "steam_whistle/block_medium_wall.json": {
        "groups": ["Whistle Medium", "medium_top_rim"],
        "elements": [],
    },
    "steam_whistle/block_large_floor.json": {
        "groups": ["Whistle Large", "large_top_rim"],
        "elements": [],
    },
    "steam_whistle/block_large_wall.json": {
        "groups": ["Whistle Large", "large_top_rim"],
        "elements": [],
    },
    "steam_whistle/medium_mouth.json": {"groups": ["Whistle Medium"], "elements": []},
    "steam_whistle/large_mouth.json": {"groups": ["Whistle Large"], "elements": []},
    "steam_whistle/small_mouth.json": {"groups": ["Whistle Small"], "elements": []},
    "steam_whistle/extension/small_bottom_rim.json": {
        "groups": ["Whistle Small"],
        "elements": [],
    },
    "steam_whistle/extension/small_single.json": {
        "groups": ["Whistle Small"],
        "elements": [],
    },
    "steam_whistle/extension/small_top_rim.json": {
        "groups": ["Whistle Small"],
        "elements": [],
    },
    "steam_whistle/extension/small_double.json": {
        "groups": ["Whistle Small"],
        "elements": [],
    },
    "steam_whistle/extension/medium_bottom_rim.json": {
        "groups": ["Whistle Medium"],
        "elements": [],
    },
    "steam_whistle/extension/medium_single.json": {
        "groups": ["Whistle Medium"],
        "elements": [],
    },
    "steam_whistle/extension/medium_top_rim.json": {
        "groups": ["Whistle Medium"],
        "elements": [],
    },
    "steam_whistle/extension/medium_double.json": {
        "groups": ["Whistle Medium"],
        "elements": [],
    },
    "steam_whistle/extension/large_bottom_rim.json": {
        "groups": ["Whistle Large"],
        "elements": [],
    },
    "steam_whistle/extension/large_single.json": {
        "groups": ["Whistle Large"],
        "elements": [],
    },
    "steam_whistle/extension/large_top_rim.json": {
        "groups": ["Whistle Large"],
        "elements": [],
    },
    "steam_whistle/extension/large_double.json": {
        "groups": ["Whistle Large"],
        "elements": [],
    },
    "steam_whistle/item.json": {"groups": ["Whistle Medium"], "elements": []},
    # Haunted bell (Bell moves)
    "haunted_bell.json": {"groups": ["Bell"], "elements": []},
    "haunted_bell_floor.json": {"groups": ["Bell"], "elements": []},
    "haunted_bell_ceiling.json": {"groups": ["Bell"], "elements": []},
    "haunted_bell_single_wall.json": {"groups": ["Bell"], "elements": []},
    "haunted_bell_double_wall.json": {"groups": ["Bell"], "elements": []},
    # Peculiar bell (Bell moves)
    "peculiar_bell.json": {"groups": ["Bell"], "elements": []},
    "peculiar_bell_floor.json": {"groups": ["Bell"], "elements": []},
    "peculiar_bell_ceiling.json": {"groups": ["Bell"], "elements": []},
    "peculiar_bell_single_wall.json": {"groups": ["Bell"], "elements": []},
    # Bell base (Frame - stationary?)
    "bell_base/block_single_wall.json": {"groups": [], "elements": []},
    "bell_base/block_double_wall.json": {"groups": [], "elements": []},
    # Ladder (Hoops - decorative)
    "ladder.json": {"groups": [], "elements": []},
    # Belt (Top, Bottom - stationary?)
    "belt/diagonal_middle.json": {"groups": [], "elements": []},
    # Table cloth (directional parts - stationary)
    "table_cloth/block.json": {"groups": [], "elements": []},
    # Packager/Repackager (sled, door move)
    "packager/tray.json": {"groups": ["sled"], "elements": []},
    "packager/hatch_closed.json": {"groups": ["door"], "elements": []},
    "packager/item.json": {"groups": ["door", "sled"], "elements": []},
    "repackager/tray.json": {"groups": ["sled"], "elements": []},
    "repackager/block_vertical.json": {"groups": ["door"], "elements": []},
    "repackager/item.json": {"groups": ["door", "sled"], "elements": []},
    # Portable storage/fluid interface (Middle moves)
    "portable_storage_interface/block_middle.json": {
        "groups": ["Middle"],
        "elements": [],
    },
    "portable_storage_interface/block_middle_powered.json": {
        "groups": ["Middle"],
        "elements": [],
    },
    "portable_storage_interface/block_top.json": {"groups": ["Top"], "elements": []},
    "portable_storage_interface/item.json": {"groups": ["Middle"], "elements": []},
    "portable_fluid_interface/block_middle.json": {
        "groups": ["Middle"],
        "elements": [],
    },
    "portable_fluid_interface/block_middle_powered.json": {
        "groups": ["Middle"],
        "elements": [],
    },
    "portable_fluid_interface/item.json": {"groups": ["Middle"], "elements": []},
    # Stock ticker (base - stationary?)
    "stock_ticker.json": {"groups": [], "elements": []},
    # Rotation speed controller (stationary)
    "rotation_speed_controller/block.json": {"groups": [], "elements": []},
    # Sequenced gearshift (block rotates?)
    "sequenced_gearshift/idle.json": {"groups": ["block"], "elements": []},
    "sequenced_gearshift/item.json": {"groups": ["encased_shaft"], "elements": []},
    # Smart chute (stationary)
    "smart_chute/block.json": {"groups": [], "elements": []},
    "smart_chute/block_powered.json": {"groups": [], "elements": []},
    # Mechanical arm claw_base_goggles (Goggles - decorative)
    "mechanical_arm/claw_base_goggles.json": {"groups": [], "elements": []},
    # Factory gauge (stationary)
    "factory_gauge/panel_with_bulb.json": {"groups": [], "elements": []},
    "factory_gauge/panel.json": {"groups": [], "elements": []},
    "factory_gauge/panel_restocker.json": {"groups": [], "elements": []},
    "factory_gauge/panel_restocker_with_bulb.json": {"groups": [], "elements": []},
    "factory_gauge/item.json": {"groups": [], "elements": []},
    # Display link (bulb - stationary)
    "display_link/model_for_editing.json": {"groups": [], "elements": []},
    "display_link/glow.json": {"groups": [], "elements": []},
    "display_link/tube.json": {"groups": [], "elements": []},
    # Symmetry effect (stationary)
    "symmetry_effect/tripleplane.json": {"groups": [], "elements": []},
    "symmetry_effect/plane.json": {"groups": [], "elements": []},
    # Gantry carriage vertical/horizontal (stationary)
    "gantry_carriage/vertical.json": {"groups": [], "elements": []},
    "gantry_carriage/horizontal.json": {"groups": [], "elements": []},
    # Metal girder encased shaft (Bearing rotates?)
    "metal_girder_encased_shaft/block.json": {"groups": ["Bearing"], "elements": []},
    "metal_girder_encased_shaft/block_bottom.json": {"groups": [], "elements": []},
    "metal_girder_encased_shaft/block_top.json": {"groups": [], "elements": []},
    # Metal girder (stationary)
    "metal_girder/block_x.json": {"groups": [], "elements": []},
    "metal_girder/block_z.json": {"groups": [], "elements": []},
    "metal_girder/block_pole.json": {"groups": [], "elements": []},
    "metal_girder/block_bottom.json": {"groups": [], "elements": []},
    "metal_girder/block_top.json": {"groups": [], "elements": []},
    "metal_girder/block_cross.json": {"groups": [], "elements": []},
    "metal_girder/segment_top.json": {"groups": [], "elements": []},
    "metal_girder/segment_middle.json": {"groups": [], "elements": []},
    "metal_girder/segment_bottom.json": {"groups": [], "elements": []},
    "metal_girder/bracket_west.json": {"groups": [], "elements": []},
    "metal_girder/bracket_east.json": {"groups": [], "elements": []},
    "metal_girder/bracket_north.json": {"groups": [], "elements": []},
    "metal_girder/bracket_south.json": {"groups": [], "elements": []},
    # Track overlay (stationary)
    "track_overlay/station.json": {"groups": [], "elements": []},
    # Stock link (stationary)
    "stock_link/vertical_for_editing.json": {"groups": [], "elements": []},
    "stock_link/horizontal_for_editing.json": {"groups": [], "elements": []},
    # Chute (stationary)
    "chute/block_intersection.json": {"groups": [], "elements": []},
    "chute/block.json": {"groups": [], "elements": []},
    "chute/block_diagonal_intersection.json": {"groups": [], "elements": []},
    "chute/block_windowed.json": {"groups": [], "elements": []},
    "chute/block_diagonal_encased.json": {"groups": [], "elements": []},
    "chute/block_diagonal.json": {"groups": [], "elements": []},
    # Basin (stationary)
    "basin/block.json": {"groups": [], "elements": []},
    "basin/block_directional.json": {"groups": [], "elements": []},
    # Cart assembler (stationary)
    "cart_assembler/item.json": {"groups": [], "elements": []},
    # Gearshift (stationary)
    "gearshift/item.json": {"groups": [], "elements": []},
    # Belt casing (stationary)
    "belt_casing/sideways_end.json": {"groups": [], "elements": []},
    "belt_casing/sideways_pulley.json": {"groups": [], "elements": []},
    "belt_casing/sideways_middle.json": {"groups": [], "elements": []},
    "belt_casing/sideways_start.json": {"groups": [], "elements": []},
    "belt_casing/horizontal_end.json": {"groups": [], "elements": []},
    "belt_casing/horizontal_pulley.json": {"groups": [], "elements": []},
    "belt_casing/horizontal_middle.json": {"groups": [], "elements": []},
    "belt_casing/horizontal_start.json": {"groups": [], "elements": []},
    "belt_casing/diagonal_end.json": {"groups": [], "elements": []},
    "belt_casing/diagonal_pulley.json": {"groups": [], "elements": []},
    "belt_casing/diagonal_middle.json": {"groups": [], "elements": []},
    "belt_casing/diagonal_start.json": {"groups": [], "elements": []},
    # Schematicannon (stationary)
    "schematicannon/block.json": {"groups": [], "elements": []},
    "schematicannon/pipe.json": {"groups": [], "elements": []},
    "schematicannon/connector.json": {"groups": [], "elements": []},
    "schematicannon/item.json": {"groups": [], "elements": []},
    # Encased shaft block variants (stationary casings)
    "encased_shaft/block.json": {"groups": [], "elements": []},
    "encased_shaft/block_andesite.json": {"groups": [], "elements": []},
    "encased_shaft/block_brass.json": {"groups": [], "elements": []},
    # Steam engine block (Housing stationary)
    "steam_engine/block.json": {"groups": [], "elements": []},
    "steam_engine/gauge.json": {"groups": [], "elements": []},
    # Mechanical piston normal/sticky block (stationary base)
    "mechanical_piston/normal/block.json": {"groups": [], "elements": []},
    "mechanical_piston/normal/block_rotated.json": {"groups": [], "elements": []},
    "mechanical_piston/sticky/block.json": {"groups": [], "elements": []},
    "mechanical_piston/sticky/block_rotated.json": {"groups": [], "elements": []},
    # Encased cogwheel block variants (stationary casings)
    "encased_cogwheel/block.json": {"groups": [], "elements": []},
    "encased_cogwheel/block_top.json": {"groups": [], "elements": []},
    "encased_cogwheel/block_bottom.json": {"groups": [], "elements": []},
    "encased_cogwheel/block_top_bottom.json": {"groups": [], "elements": []},
    # Mechanical roller block/frame (stationary)
    "mechanical_roller/block.json": {"groups": [], "elements": []},
    "mechanical_roller/frame.json": {"groups": [], "elements": []},
    # Chain conveyor block (stationary)
    "chain_conveyor/block.json": {"groups": [], "elements": []},
    "chain_conveyor/guard.json": {"groups": [], "elements": []},
    "chain_conveyor/textures.json": {"groups": [], "elements": []},
    # Mechanical crafter block (stationary)
    "mechanical_crafter/block.json": {"groups": [], "elements": []},
    # Mechanical harvester block (stationary)
    "mechanical_harvester/block.json": {"groups": [], "elements": []},
    # Mechanical pump block (stationary)
    "mechanical_pump/block.json": {"groups": [], "elements": []},
    # Weighted ejector block (stationary)
    "weighted_ejector/block.json": {"groups": ["casing"], "elements": []},
    # Hand crank block (stationary)
    "hand_crank/block.json": {"groups": ["shaft_half"], "elements": []},
    # Bearing block (stationary)
    "bearing/block.json": {"groups": [], "elements": []},
    # Encased fan block (stationary)
    "encased_fan/block.json": {"groups": [], "elements": []},
    # Millstone block (stationary)
    "millstone/block.json": {"groups": ["bottom"], "elements": []},
    # Flywheel block (stationary housing)
    "flywheel/block.json": {"groups": [], "elements": []},
    # Large water wheel block (stationary)
    "large_water_wheel/block.json": {"groups": [], "elements": []},
    "large_water_wheel/textures.json": {"groups": [], "elements": []},
    "large_water_wheel/item.json": {"groups": [], "elements": []},
    # Water wheel block (stationary)
    "water_wheel/block.json": {"groups": [], "elements": []},
    "water_wheel/textures.json": {"groups": [], "elements": []},
    "water_wheel/item.json": {"groups": [], "elements": []},
    # Blaze burner block variants (stationary)
    "blaze_burner/block.json": {"groups": [], "elements": []},
    "blaze_burner/block_with_fire.json": {"groups": [], "elements": []},
    "blaze_burner/block_with_blaze.json": {"groups": [], "elements": []},
    "blaze_burner/block_with_soul_fire.json": {"groups": [], "elements": []},
    # Funnel block variants (stationary)
    "funnel/block_vertical_filterless.json": {"groups": [], "elements": []},
    "funnel/block_horizontal.json": {"groups": [], "elements": []},
    "funnel/block_vertical.json": {"groups": [], "elements": []},
    # Spout block (stationary)
    "spout/block.json": {"groups": [], "elements": []},
    # Gearbox block (stationary)
    "gearbox/block.json": {"groups": [], "elements": []},
    # Mechanical drill block (stationary)
    "mechanical_drill/block.json": {"groups": ["casing"], "elements": []},
    # Mechanical arm block (stationary)
    "mechanical_arm/block.json": {"groups": [], "elements": []},
}

# Aeronautics kinetic models
AERONAUTICS_KINETIC_MODELS = {
    # Propellers rotate
    "andesite_propeller/propeller.json",
    "andesite_propeller/propeller_reversed.json",
    "wooden_propeller/propeller.json",
    "wooden_propeller/propeller_reversed.json",
    "smart_propeller/propeller.json",
    "smart_propeller/propeller_reversed.json",
    "propeller_bearing/bearing_plate.json",  # rotates?
    # Gyroscopic propeller bearing (piston parts move)
    "gyroscopic_propeller_bearing/piston_head.json",
    "gyroscopic_propeller_bearing/piston_pole.json",
    "gyroscopic_propeller_bearing/metal_bearing_plate.json",
    # Mounted potato cannon (cog rotates)
    "mounted_potato_cannon/partials/cog.json",
    "mounted_potato_cannon/partials/barrel.json",
    "mounted_potato_cannon/partials/bellow.json",
    # Adjustable burner (fire - not kinetic)
    # Steam vent (steam_jet - particle)
    "steam_vent/steam_jet.json",
    # Envelope encased shaft (shaft rotates)
    "envelope_encased_shaft/block.json",  # if shaft rotates
}

AERONAUTICS_PARTIALLY_ROTATING = {
    "andesite_propeller/block.json": {"groups": [], "elements": []},
    "wooden_propeller/block.json": {"groups": [], "elements": []},
    "smart_propeller/block.json": {
        "groups": ["hinge"],  # hinge moves
        "elements": [],
    },
    "smart_propeller/hinge.json": {"groups": [], "elements": []},
    "propeller_bearing/block.json": {
        "groups": ["core"],  # core rotates
        "elements": [],
    },
    "propeller_bearing/item.json": {
        "groups": ["prophead"],  # prophead rotates
        "elements": [],
    },
    "mounted_potato_cannon/block_horizontal.json": {"groups": [], "elements": []},
    "mounted_potato_cannon/block_horizontal_powered.json": {
        "groups": [],
        "elements": [],
    },
    "mounted_potato_cannon/block_vertical.json": {"groups": [], "elements": []},
    "mounted_potato_cannon/block_vertical_powered.json": {"groups": [], "elements": []},
    "mounted_potato_cannon/item.json": {
        "groups": ["Gear"],  # gears rotate
        "elements": [],
    },
    "adjustable_burner/block_fire.json": {"groups": [], "elements": []},
    "adjustable_burner/block_powered.json": {"groups": [], "elements": []},
    "adjustable_burner/block_soulful.json": {"groups": [], "elements": []},
    "adjustable_burner/fire.json": {"groups": [], "elements": []},
    "adjustable_burner/item.json": {"groups": [], "elements": []},
    "adjustable_burner/redstone_indicator.json": {"groups": [], "elements": []},
    "steam_vent/block_gold.json": {"groups": [], "elements": []},
    "steam_vent/block_iron.json": {"groups": [], "elements": []},
    "steam_vent/item.json": {"groups": [], "elements": []},
    "steam_vent/redstone.json": {"groups": [], "elements": []},
    "steam_vent/steam_base.json": {"groups": [], "elements": []},
    "envelope_encased_shaft/block.json": {
        "groups": [],  # check if shaft rotates
        "elements": [],
    },
    "envelope_encased_shaft/item.json": {"groups": [], "elements": []},
    "gyroscopic_propeller_bearing/block.json": {"groups": [], "elements": []},
    "gyroscopic_propeller_bearing/item.json": {"groups": [], "elements": []},
}

# Item models that reference kinetic block models (need updating)
# These are handled separately


def process_jar(
    jar_path, output_path, kinetic_files, partially_rotating, mod_prefix="create"
):
    """Process a JAR file to add kinetic_ prefixes."""
    print(f"\nProcessing {jar_path}...")

    # Extract to temp directory
    temp_dir = Path("temp_extract") / mod_prefix
    if temp_dir.exists():
        shutil.rmtree(temp_dir)
    temp_dir.mkdir(parents=True)

    with zipfile.ZipFile(jar_path, "r") as z:
        # For Aeronautics, only extract JSON files to avoid long path issues
        if mod_prefix == "aeronautics":
            for member in z.namelist():
                if (
                    member.endswith(".json")
                    or member.endswith(".png")
                    or member.endswith(".mcmeta")
                    or "META-INF" in member
                ):
                    z.extract(member, temp_dir)
        else:
            z.extractall(temp_dir)

    models_dir = temp_dir / "assets" / mod_prefix / "models"
    if not models_dir.exists():
        models_dir = temp_dir / "assets" / "minecraft" / "models"  # fallback

    # Track renames for reference updates
    file_renames = {}  # old_path -> new_path

    # ============================================================
    # STEP 1: Rename fully kinetic model files
    # ============================================================
    for rel_path in kinetic_files:
        full_path = models_dir / "block" / rel_path
        if full_path.exists():
            new_name = "kinetic_" + full_path.name
            new_path = full_path.parent / new_name
            print(f"  Renaming: {rel_path} -> {new_name}")
            full_path.rename(new_path)
            file_renames[rel_path] = str(new_path.relative_to(models_dir / "block"))
        else:
            print(f"  WARNING: Not found: {rel_path}")

    # ============================================================
    # STEP 2: Update partially rotating models (prefix group/element names)
    # ============================================================
    for rel_path, config in partially_rotating.items():
        full_path = models_dir / "block" / rel_path
        if full_path.exists():
            with open(full_path, "r", encoding="utf-8") as f:
                data = json.load(f)

            modified = False

            # Prefix group names
            if "groups" in data and isinstance(data["groups"], list):
                for group in data["groups"]:
                    if isinstance(group, dict) and "name" in group:
                        old_name = group["name"]
                        if old_name in config["groups"]:
                            group["name"] = "kinetic_" + old_name
                            print(
                                f"  Prefixing group in {rel_path}: {old_name} -> kinetic_{old_name}"
                            )
                            modified = True

            # Prefix element names
            if "elements" in data and isinstance(data["elements"], list):
                for element in data["elements"]:
                    if isinstance(element, dict) and "name" in element:
                        old_name = element["name"]
                        if old_name in config["elements"]:
                            element["name"] = "kinetic_" + old_name
                            print(
                                f"  Prefixing element in {rel_path}: {old_name} -> kinetic_{old_name}"
                            )
                            modified = True

            if modified:
                with open(full_path, "w", encoding="utf-8") as f:
                    json.dump(data, f, indent=1, ensure_ascii=False)
        else:
            print(f"  WARNING: Partially rotating model not found: {rel_path}")

    # ============================================================
    # STEP 3: Update references in other files (blockstates, item models, etc.)
    # ============================================================
    # This is complex - we'd need to update:
    # - Blockstate JSON files that reference the renamed models
    # - Item models that reference the renamed models
    # - Other model files that use "parent" to reference renamed models

    # For now, let's just update model files that reference renamed models via "parent"
    for model_file in (models_dir / "block").rglob("*.json"):
        if model_file.name.startswith("kinetic_"):
            continue  # skip already renamed files
        try:
            with open(model_file, "r", encoding="utf-8") as f:
                data = json.load(f)
            modified = False

            # Update parent reference
            if "parent" in data:
                parent = data["parent"]
                for old_rel, new_rel in file_renames.items():
                    old_parent = f"{mod_prefix}:block/{old_rel[:-5]}"  # remove .json
                    new_parent = f"{mod_prefix}:block/{new_rel[:-5]}"
                    # Normalize path separators
                    old_parent = old_parent.replace("\\", "/")
                    new_parent = new_parent.replace("\\", "/")
                    parent_norm = parent.replace("\\", "/")
                    if parent_norm == old_parent:
                        data["parent"] = new_parent
                        print(
                            f"  Updating parent in {model_file.relative_to(models_dir / 'block')}: {old_parent} -> {new_parent}"
                        )
                        modified = True

            if modified:
                with open(model_file, "w", encoding="utf-8") as f:
                    json.dump(data, f, indent=1, ensure_ascii=False)
        except Exception as e:
            print(f"  Error processing {model_file}: {e}")

    # ============================================================
    # STEP 4: Repackage JAR
    # ============================================================
    print(f"  Repackaging to {output_path}...")
    with zipfile.ZipFile(jar_path, "r") as zin:
        with zipfile.ZipFile(output_path, "w", zipfile.ZIP_DEFLATED) as zout:
            # Track which renamed files have been written
            written_renamed = set()

            # Copy all files from original JAR
            for item in zin.infolist():
                if mod_prefix == "aeronautics" and item.filename.endswith(".class"):
                    # Copy class files directly
                    data = zin.read(item.filename)
                    zout.writestr(item, data)
                elif item.filename.endswith(".json") and "models" in item.filename:
                    # For model JSON files, check if we have a modified version
                    base_path = Path("assets") / mod_prefix / "models" / "block"
                    try:
                        rel_path = Path(item.filename).relative_to(base_path)
                    except ValueError:
                        # Not under block models, copy as-is
                        data = zin.read(item.filename)
                        zout.writestr(item, data)
                        continue
                    # Check if this file was renamed
                    renamed = False
                    for old_rel, new_rel in file_renames.items():
                        if str(rel_path) == old_rel:
                            # This file was renamed, write the new file with new name
                            new_full_path = models_dir / "block" / new_rel
                            if new_full_path.exists():
                                with open(new_full_path, "rb") as f:
                                    zout.writestr(
                                        f"assets/{mod_prefix}/models/block/{new_rel}",
                                        f.read(),
                                    )
                                written_renamed.add(new_rel)
                            renamed = True
                            break
                    if not renamed:
                        # Check if this is a modified partially rotating model
                        modified_path = models_dir / "block" / rel_path
                        if modified_path.exists():
                            with open(modified_path, "rb") as f:
                                zout.writestr(item.filename, f.read())
                        else:
                            # Copy original
                            data = zin.read(item.filename)
                            zout.writestr(item, data)
                else:
                    # Copy all other files as-is
                    data = zin.read(item.filename)
                    zout.writestr(item, data)

            # Write any renamed files that weren't in the original JAR (e.g., new kinetic_ files)
            for old_rel, new_rel in file_renames.items():
                if new_rel not in written_renamed:
                    new_full_path = models_dir / "block" / new_rel
                    if new_full_path.exists():
                        with open(new_full_path, "rb") as f:
                            zout.writestr(
                                f"assets/{mod_prefix}/models/block/{new_rel}",
                                f.read(),
                            )
                        print(f"  Adding new renamed file: {new_rel}")

    # Cleanup
    shutil.rmtree(temp_dir.parent)
    print(f"  Done!")


def main():
    # Process main Create JAR
    process_jar(
        "create-1.21.1-6.0.10.jar",
        "create-1.21.1-6.0.10-kinetic.jar",
        KINETIC_MODEL_FILES,
        PARTIALLY_ROTATING_MODELS,
        "create",
    )

    # Process Aeronautics JAR
    # First extract the inner JAR
    print("\nExtracting Aeronautics inner JAR...")
    import tempfile

    with tempfile.TemporaryDirectory() as tmpdir:
        inner_jar = Path(tmpdir) / "aeronautics.jar"
        with zipfile.ZipFile("create-aeronautics-bundled-1.21.1-1.3.2.jar", "r") as z:
            # Extract only the inner JAR
            for member in z.namelist():
                if member.endswith("aeronautics-neoforge-1.21.1-1.3.2.jar"):
                    with z.open(member) as src, open(inner_jar, "wb") as dst:
                        dst.write(src.read())
                    break

        process_jar(
            str(inner_jar),
            "create-aeronautics-bundled-1.21.1-1.3.2-kinetic.jar",
            AERONAUTICS_KINETIC_MODELS,
            AERONAUTICS_PARTIALLY_ROTATING,
            "aeronautics",
        )

    # Cleanup
    if Path("temp_aero").exists():
        shutil.rmtree("temp_aero")

    print("\nAll done!")


if __name__ == "__main__":
    main()
