// Edge-directed upscaling of the 320x200 cutscene frames at display time, used when HD art is on
// (see HdArt and cutsplayer.ApplyHdUpscale). The cutscene player composes, fades and colour-cycles
// its frames at the original resolution, so they are smoothed here, where all of that has already
// happened, rather than replaced like the rest of the art.
//
// The rules are xBR level 1's (Hyllian): for the texel corner the fragment is nearest to, compare
// the colour distances along the two diagonals; if the edge runs across that corner, the corner
// past the line through the mid-points of its two sides takes the closer of the two neighbours.
// The line is antialiased over about one screen pixel. No new colours are made beyond that blend.
//
// Textures wider than 400 texels are already HD art (the logos shown through the same control)
// and pass through unchanged. Their size is read with textureSize, the texels the image really
// has: HD art reports its original 320x200 through SetSizeOverride, and TEXTURE_PIXEL_SIZE
// follows that, which once had the logos sampled as 320x200 and showed their top left corner.

shader_type canvas_item;

// Perceptual distance between two colours, weighted in YUV as xBR does.
float dist(vec3 a, vec3 b) {
	vec3 d = a - b;
	float y = 0.299 * d.r + 0.587 * d.g + 0.114 * d.b;
	float u = -0.169 * d.r - 0.331 * d.g + 0.5 * d.b;
	float v = 0.5 * d.r - 0.419 * d.g - 0.081 * d.b;
	return 48.0 * abs(y) + 7.0 * abs(u) + 6.0 * abs(v);
}

vec4 texel(sampler2D tex, ivec2 p, ivec2 size) {
	return texelFetch(tex, clamp(p, ivec2(0), size - ivec2(1)), 0);
}

void fragment() {
	vec2 sizef = vec2(textureSize(TEXTURE, 0));
	if (sizef.x > 400.0) {
		COLOR = texture(TEXTURE, UV);
	} else {
		ivec2 size = ivec2(sizef);
		vec2 pos = UV * sizef;
		ivec2 c = ivec2(floor(pos));
		vec2 f = fract(pos) - 0.5;
		// Mirror so the corner the fragment is nearest to is the bottom-right one.
		ivec2 m = ivec2(f.x < 0.0 ? -1 : 1, f.y < 0.0 ? -1 : 1);
		vec4 tE = texel(TEXTURE, c, size);
		vec3 e = tE.rgb;
		vec3 tF = texel(TEXTURE, c + ivec2(m.x, 0), size).rgb;
		vec3 tH = texel(TEXTURE, c + ivec2(0, m.y), size).rgb;
		vec3 tI = texel(TEXTURE, c + m, size).rgb;
		vec3 tB = texel(TEXTURE, c + ivec2(0, -m.y), size).rgb;
		vec3 tD = texel(TEXTURE, c + ivec2(-m.x, 0), size).rgb;
		vec3 tC = texel(TEXTURE, c + ivec2(m.x, -m.y), size).rgb;
		vec3 tG = texel(TEXTURE, c + ivec2(-m.x, m.y), size).rgb;
		vec3 tF4 = texel(TEXTURE, c + ivec2(2 * m.x, 0), size).rgb;
		vec3 tI4 = texel(TEXTURE, c + ivec2(2 * m.x, m.y), size).rgb;
		vec3 tH5 = texel(TEXTURE, c + ivec2(0, 2 * m.y), size).rgb;
		vec3 tI5 = texel(TEXTURE, c + ivec2(m.x, 2 * m.y), size).rgb;

		float across = dist(e, tC) + dist(e, tG) + dist(tI, tF4) + dist(tI, tH5) + 4.0 * dist(tH, tF);
		float along = dist(tH, tD) + dist(tH, tI5) + dist(tF, tI4) + dist(tF, tB) + 4.0 * dist(e, tI);
		bool edge = across < along && dist(e, tF) > 0.01 && dist(e, tH) > 0.01;

		vec4 outc = tE;
		if (edge) {
			vec3 p = dist(e, tF) <= dist(e, tH) ? tF : tH;
			float d = abs(f.x) + abs(f.y);
			float w = max(fwidth(d), 1e-4);
			outc = vec4(mix(e, p, smoothstep(0.5 - w, 0.5 + w, d)), tE.a);
		}
		COLOR = outc;
	}
}
