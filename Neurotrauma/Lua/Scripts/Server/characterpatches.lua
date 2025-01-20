-- Hooks CalculateMovementPenalty method of Barotrauma.Character
-- when painless enough, disable weapon sway / movement hindrance limb penalties

Hook.Patch("Barotrauma.Character", "CalculateMovementPenalty", function(instance, ptable)
	if HF.HasAffliction(instance, "analgesia", 20) then
		ptable.PreventExecution = true
		return 0
	end
end, Hook.HookMethodType.Before)

Hook.Patch("Barotrauma.AnimController", "GetAimWobble", function(instance, ptable)
	if HF.HasAffliction(instance.Character, "analgesia", 20) then
		ptable.PreventExecution = true
		return 0
	end
end, Hook.HookMethodType.Before)
