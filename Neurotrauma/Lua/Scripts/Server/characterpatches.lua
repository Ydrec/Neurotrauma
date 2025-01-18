-- Hooks CalculateMovementPenalty method of Barotrauma.Character
-- when painless enough, disable weapon sway / movement hindrance limb penalties

local ignoredLimbs = {
	LimbType.LeftFoot,
	LimbType.RightFoot,
	LimbType.LeftHand,
	LimbType.RightHand,
}

Hook.Patch("Barotrauma.Character", "CalculateMovementPenalty", function(instance, ptable)
	if HF.TableContains(ignoredLimbs, ptable["limb"].type) and HF.HasAffliction(instance, "analgesia", 20) then
		ptable.PreventExecution = true
		return 0
	end
end, Hook.HookMethodType.Before)
