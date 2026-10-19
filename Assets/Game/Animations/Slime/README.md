# Slime animation assets

`PF_Slime_Green` keeps its gameplay root at 64 PPU. Its `Visual` child uses the original, untinted 100 PPU sprites at scale 0.45, so changing presentation artwork does not change movement, perception, or mechanism ranges.

The normal set contains Idle, Move, Attack, Eat, Furnace, Hit, and Collision clips. Each clip uses 120 frames at 24 fps. Idle and Move loop; the other actions play once. Death keeps the existing 0.35 second fade while waiting for dedicated art.

The seven source action folders are imported as single sprites at 100 PPU without texture compression. The included sprite atlas applies platform compression after packing, avoiding lossy compression of the source frames before atlas generation.

## Future status art

No state-specific art is present yet. Afraid, Hungry, Cold, Frozen, Burning, Agitated, and Hot currently use the Normal clips and report a warning when first activated. To add a status set, create an `AnimatorOverrideController` based on `AC_Slime_Normal`, replace the available action clips, then add it to `SlimeVisualPresenter.stateOverrides` with the matching `SlimeNeedState`. Unreplaced actions keep their Normal clips.

When statuses overlap, the displayed status follows the actor's need priority: Afraid, Hungry, Cold, Frozen, Burning, Agitated, Hot. Attack, hit, food, furnace, and collision actions temporarily play over that status set. A death still stops the Animator and fades the sprite.
