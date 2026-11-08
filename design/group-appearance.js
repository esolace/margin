// Browser adapter for the shared policy. Native adapters are checked against it.
globalThis.MarginGroupAppearance = Object.freeze({
  resolve(style, canvas, policy) {
    const a = style.Appearance;
    const rgb = hex => hex.slice(1).match(/../g).map(x => parseInt(x, 16));
    const hex = channels => '#' + channels.map(x => Math.floor(x).toString(16).padStart(2, '0')).join('').toUpperCase();
    const luminance = channels => channels.reduce((sum, x, i) => sum + x * policy.luminanceWeights[i], 0) / 255;
    const fill = rgb(style.Fill), background = rgb(canvas), alpha = style.Opacity / 100;
    const edge = hex(fill.map(x => luminance(fill) > policy.lightThreshold
      ? x * policy.lightEdgeScale : Math.min(255, x + policy.darkEdgeOffset)));
    const accent = a.Accent || edge, heading = policy.headings[a.Heading];
    const behind = heading.band ? rgb(accent) : fill.map((x, i) => x * alpha + background[i] * (1 - alpha));
    return {
      rgb: fill, accent, border: a.Border || edge,
      ink: a.Text || (luminance(behind) > policy.lightThreshold ? policy.darkInk : policy.lightInk),
      band: heading.band, divider: heading.divider && a.ShowDivider
    };
  }
});
