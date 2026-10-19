# Slime animation assets

`PF_Slime_Green` keeps its gameplay root at 64 PPU. Its `Visual` child uses the original, untinted 100 PPU sprites at scale 0.45, so changing presentation artwork does not change movement, perception, or mechanism ranges.

All animation sequences contain 120 frames at 24 fps and play for 5 seconds. Idle and Move loop; interaction, hit, collision, and death clips play once. The seven normal locomotion/action clips remain in `AC_Slime_Normal`; the normal death clip is in `AC_Slime_Normal` as well.

## Status animation sets

`SlimeVisualPresenter.stateOverrides` selects the animation controller for the highest-priority active status. Available state art is connected as follows:

- **Afraid:** purple Idle, Move, Attack, Eat, Furnace, Hit, Collision, and Death.
- **Hungry:** Idle, Move (`移动_新版`), Attack, Eat, Furnace, Hit, Collision, and Death.
- **Agitated:** Move, Attack, Eat, Furnace, Hit, and Collision. Idle and Death fall back to the normal clips because those state-specific sequences are not present.
- **Cold, Frozen, Burning, Hot:** no state-specific sequences are present yet; they use the normal clips.

When multiple statuses are active, the displayed set follows the actor's need priority: Afraid, Hungry, Cold, Frozen, Burning, Agitated, Hot. Any missing action uses the normal clip for that action. Death plays the active set's death clip when available, then uses the existing 0.35 second fade. Missing state-specific death art falls back to the normal death clip.

The normal set uses `SA_Slime_Normal`. State frames use matching atlases: `SA_Slime_Normal_Death`, `SA_Slime_Hungry`, `SA_Slime_Afraid`, and `SA_Slime_Agitated`. These atlases follow the normal atlas's tight packing and platform compression settings; the source sprites remain at 100 PPU.
