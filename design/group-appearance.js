// Browser adapter for the shared policy. Native adapters are checked against it.
globalThis.MarginGroupAppearance = Object.freeze({
  formatTitle(name, casing, policy) {
    if (!Object.hasOwn(policy.titleCases, casing)) throw new Error('Choose a valid title case.');
    if (casing === 'AsTyped') return name;
    // Greek full-uppercase expansions need their Unicode simple-case counterparts.
    const simple = (letter, upper) => {const override=policy.browserSimpleCaseOverrides[upper?'upper':'lower'][letter];if(override)return override;const mapped = upper ? letter.toUpperCase() : letter.toLowerCase();return [...mapped].length === 1 ? mapped : letter;};
    let initial = true, result = '';
    for (const letter of name) {
      let value = simple(letter, casing === 'Uppercase');
      if (casing === 'TitleCase' || casing === 'SentenceCase') {
        if (/\p{L}/u.test(letter)) {if(initial)value=simple(value,true);initial=false;}
        else if(casing === 'TitleCase' && !/[\p{N}\p{M}'’]/u.test(letter)) initial=true;
      }
      result += value;
    }
    return result;
  },
  resolve(style, canvas, policy, externalTitle = false) {
    const a = style.Appearance;
    const rgb = hex => hex.slice(1).match(/../g).map(x => parseInt(x, 16));
    const hex = channels => '#' + channels.map(x => Math.floor(x).toString(16).padStart(2, '0')).join('').toUpperCase();
    const luminance = channels => channels.reduce((sum, x, i) => sum + x * policy.luminanceWeights[i], 0) / 255;
    const fill = rgb(style.Fill), background = rgb(canvas), alpha = style.Opacity / 100;
    const edge = hex(fill.map(x => luminance(fill) > policy.lightThreshold
      ? x * policy.lightEdgeScale : Math.min(255, x + policy.darkEdgeOffset)));
    const accent = a.Accent || edge, heading = policy.headings[a.Heading];
    const behind = externalTitle ? background : heading.band ? rgb(accent) : fill.map((x, i) => x * alpha + background[i] * (1 - alpha));
    return {
      rgb: fill, accent, border: a.Border || edge,
      ink: a.Text || (luminance(behind) > policy.lightThreshold ? policy.darkInk : policy.lightInk),
      band: !externalTitle && heading.band, divider: !externalTitle && heading.divider && a.ShowDivider
    };
  }
});
