-- Hooks CalculateMovementPenalty method of Barotrauma.Character
-- when painless enough, disable weapon sway / movement hindrance limb penalties

-- Disable movement penalties for painless characters
Hook.Patch("Barotrauma.Character", "CalculateMovementPenalty", function(instance, ptable)
	if HF.HasAffliction(instance, "analgesia", 20) then
		ptable.PreventExecution = true
		return 0
	end
end, Hook.HookMethodType.Before)

-- Disable aim penalties for painless characters
Hook.Patch("Barotrauma.AnimController", "GetAimWobble", function(instance, ptable)
	if HF.HasAffliction(instance.Character, "analgesia", 20) then
		ptable.PreventExecution = true
		return 0
	end
end, Hook.HookMethodType.Before)

-- Patch to cause unconscious from the game rather than stun
Hook.Patch("Barotrauma.CharacterHealth", "get_IsUnconscious", function(instance, ptable)
	local isUnconscious = HF.HasAffliction(instance.Character, "sym_unconsciousness")
	ptable.PreventExecution = true
	return instance.Character.IsDead
		or (
			(instance.Character.Vitality <= 0.0 or isUnconscious)
			and not instance.Character.HasAbilityFlag(AbilityFlags.AlwaysStayConscious)
		)
end, Hook.HookMethodType.After)
