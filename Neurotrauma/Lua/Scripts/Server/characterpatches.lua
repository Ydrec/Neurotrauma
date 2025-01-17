local ignoredLimbs = {
	LimbType.LeftFoot,
	LimbType.RightFoot,
	LimbType.LeftThigh,
	LimbType.RightThigh,
	LimbType.LeftLeg,
	LimbType.RightLeg,
}
Hook.Patch("Barotrauma.Character", "GetLegPenalty", function(instance, ptable)
	ptable["startSum"] = Single(-99999)
end, Hook.HookMethodType.Before)

Hook.Patch("Barotrauma.Character", "CalculateMovementPenalty", function(instance, ptable)
	if HF.TableContains(ignoredLimbs, ptable["limb"].type) then
		ptable.PreventExecution = true
		return 0
	end
end, Hook.HookMethodType.Before)

