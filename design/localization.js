// Shared offline catalogs; only explicitly marked interface text is translated.
(() => {
  const catalogs = JSON.parse(document.getElementById('marginLocales').textContent);
  const languages = {en:'English',cs:'Čeština',de:'Deutsch',es:'Español',fr:'Français',it:'Italiano',ja:'日本語',ko:'한국어',pl:'Polski',pt:'Português',ru:'Русский','zh-Hans':'简体中文','zh-Hant':'繁體中文'};
  let choice = 'auto', host = navigator.language, language = 'en';
  function resolve(selected, actual) {
    if(selected !== 'auto' && Object.hasOwn(languages,selected)) return selected;
    const tag=(actual||'').replaceAll('_','-').toLowerCase();
    if(tag.startsWith('zh'))return tag.includes('hans')?'zh-Hans':tag.includes('hant')||/-(tw|hk|mo)$/.test(tag)?'zh-Hant':'zh-Hans';
    const base=tag.split('-')[0];return Object.hasOwn(languages,base)?base:'en';
  }
  function text(key,args={}) {
    const template=catalogs[language]?.[key]??catalogs.en[key]??key;
    return template.replace(/\{([a-zA-Z][a-zA-Z0-9]*)\}/g,(token,name)=>Object.hasOwn(args,name)?String(args[name]??''):token);
  }
  function set(next,actual=host) {
    choice=next==='auto'||Object.hasOwn(languages,next)?next:'auto'; host=actual;language=resolve(choice,host);
    document.documentElement.lang=language;
    document.querySelectorAll('[data-i18n]').forEach(node=>node.textContent=text(node.dataset.i18n));
    for(const attribute of ['aria-label','placeholder','title'])
      document.querySelectorAll('[data-i18n-'+attribute+']').forEach(node=>node.setAttribute(attribute,text(node.getAttribute('data-i18n-'+attribute))));
  }
  window.MarginI18n={languages,text,resolve,set,get choice(){return choice;},get language(){return language;},get host(){return host;}};
})();
