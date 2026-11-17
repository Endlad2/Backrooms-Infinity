log.info("example init")
generator.grid(2)

events.on("hover", "hover_cube", function(id)
  ui.text("hint", "You are looking at " .. id)
end)
