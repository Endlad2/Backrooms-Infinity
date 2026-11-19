-- flicker.lua — per-chunk light flicker.
local accum = 0
local phase = math.random() * 6.283
events.on("update", nil, function(dt)
  accum = accum + (dt or 0.016)
  local t = accum + phase
  local v = math.sin(t * 3.1) * math.sin(t * 7.7) * math.sin(t * 1.3)
  local visible = v > -0.45
  for _, id in ipairs({"lamp_a", "lamp_b", "lamp_c", "lamp_d"}) do
    if world.exists(id) then world.set_visible(id, visible) end
  end
  for _, id in ipairs({"lamp_1", "lamp_2", "lamp_3"}) do
    if world.exists(id) then world.set_visible(id, visible) end
  end
end)
