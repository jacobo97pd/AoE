# Alpha 0.2 asset provenance

The playable world uses original editable C# geometry recipes and local shader code. The full-screen main-menu illustration is a separate decorative bitmap; it is not represented as an in-game rendering capture.

## Menu illustration

- Asset: `Assets/Game/Resources/Interface/EmberfieldKeyArt.png`.
- Produced with the built-in image generation tool on 2026-09-10, using the `imagegen` skill. No external asset pack or image API credentials were used.
- The generated file was copied into the project without modifying the original. The Unity frontend loads the project copy through Resources.
- Prompt:

> Use case: stylized-concept. Asset type: original text-free key art background for the main menu of EMBERFIELD, a premium stylized medieval real-time strategy game. Create a cinematic panoramic landscape, 16:9 composition, high resolution suitable for a game menu. Golden late-afternoon sunlight falls over a lush river valley, an original terraced stone-and-timber frontier town with blue and muted teal cloth banners occupies the right half and distant centre, a graceful old stone ford/bridge over turquoise water, ochre tiled gabled roofs, cultivated fields, ancient pines and soft hazy hills, a small watchtower and distant warm camp lights. Painterly hand-crafted 3D animation-film quality, refined detailed materials, atmospheric depth, warm ivory stone, weathered wood, rich forest greens, restrained gold highlights and teal shadows. Inviting, strategic, adventurous, not childish. Strong composition: left 42 percent mostly dark softly detailed forested foreground and atmospheric negative space so live UI typography will remain readable; focal town and sun rays on right 60 percent. No text, no lettering, no logo, no frame, no buttons, no HUD, no watermark. This is decorative menu illustration, not a screenshot. Keep the lower edge dark enough for a footer. No recognizable assets, characters, insignia or architecture from existing franchises.

## Typography

The project bundles Marcellus Regular for display typography and Lato Regular/Bold for readable controls. They were downloaded from the official Google Fonts repository. The original SIL Open Font License and copyright notices are retained beside each family in `Assets/Game/Resources/Fonts/`; no font was modified. [Marcellus source and license](https://github.com/google/fonts/tree/main/ofl/marcellus), [Lato source and license](https://github.com/google/fonts/tree/main/ofl/lato).

## Native artwork

`AlphaWorldGeometry.cs` and `AlphaWorldArt.cs` define the new shared faction meshes. The existing baked art-slice catalog remains as a historical gallery and fallback. `AlphaIcon.cs` draws the interface symbols directly as uGUI vector geometry. Gradients, panel frames and button states are native UI components. No franchise assets or commercial asset-store packages are included.

The store is a product screen with a deliberately empty catalog. No item prices, currency, purchased inventory or payment processing are invented for it.
