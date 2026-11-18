import '@material/web/all.js';
import { styles as typescaleStyles } from '@material/web/typography/md-typescale-styles.js';
import { applyTheme, argbFromHex, themeFromSourceColor } from '@material/material-color-utilities';

const dark = new URLSearchParams(location.search).get('theme') === 'dark';
const theme = themeFromSourceColor(argbFromHex('#6750a4'));
applyTheme(theme, { target: document.documentElement, dark });
document.adoptedStyleSheets.push(typescaleStyles.styleSheet);
window.baselineTokens = theme.schemes[dark ? 'dark' : 'light'].toJSON();
window.baselineScope = 'Google Material Web / Material 3; not Material 2 or full Expressive coverage';
// Paths from Google's Material Icons (Apache-2.0); rendered inside the official md-icon.
const refresh = '<md-icon><svg viewBox="0 0 24 24"><path d="M17.65 6.35A7.958 7.958 0 0 0 12 4a8 8 0 1 0 7.93 9h-2.02A6 6 0 1 1 12 6c1.66 0 3.14.69 4.22 1.78L13 11h7V4z"/></svg></md-icon>';
const add = '<md-icon slot="icon"><svg viewBox="0 0 24 24"><path d="M19 13h-6v6h-2v-6H5v-2h6V5h2v6h6z"/></svg></md-icon>';
document.getElementById('root').innerHTML = `
<div class="specimen material">
  <header class="heading">
    <span class="md-typescale-label-small">OFFICIAL COMPONENT BASELINE · MATERIAL 3 WEB</span>
    <h1 class="md-typescale-headline-medium" data-measure="title">A quieter place to read</h1>
    <p class="md-typescale-body-large" data-measure="body">A reader brings subscriptions, articles, and reading preferences together.</p>
    <span class="md-typescale-body-small" data-measure="caption">Material Web · Roboto · ${dark ? 'Dark' : 'Light'}</span>
  </header>
  <main class="stack">
    <div class="row spread"><h2 class="md-typescale-title-large" data-measure="section-title">Latest articles</h2>
      <md-icon-button aria-label="Refresh feeds" data-measure="icon-button">${refresh}</md-icon-button></div>
    <md-tabs aria-label="Article filter" data-measure="navigation-tabs">
      <md-primary-tab active data-measure="tab-selected">Unread</md-primary-tab>
      <md-primary-tab>All articles</md-primary-tab><md-primary-tab>Saved</md-primary-tab>
    </md-tabs>
    <md-list data-measure="list">
      <md-list-item type="button" data-measure="list-item">
        <div slot="headline">The small details of a good reading experience</div>
        <div slot="supporting-text">Design journal · 8 minutes ago</div>
        <md-checkbox slot="start" checked aria-label="Select article" data-measure="checkbox-selected"></md-checkbox>
      </md-list-item>
      <md-list-item type="button"><div slot="headline">Building tools that leave room to think</div>
        <div slot="supporting-text">Engineering notes · Yesterday</div></md-list-item>
    </md-list>
    <p class="md-typescale-body-large" data-measure="article-body">Clear typography and calm surfaces keep attention on the story. This paragraph uses the unmodified body-large type scale.</p>
    <md-divider></md-divider>
    <h2 class="md-typescale-title-large">Controls and states</h2>
    <div class="row wrap">
      <md-filled-button data-action="primary" data-measure="primary">${add}Subscribe</md-filled-button>
      <md-outlined-button data-measure="secondary">Mark as read</md-outlined-button>
      <md-filled-button disabled data-measure="disabled">Unavailable</md-filled-button>
      <md-text-button data-action="popup">Appearance</md-text-button>
    </div>
    <div class="row wrap align-start">
      <md-outlined-text-field label="Subscription name" value="Design journal" data-action="input" data-measure="input"></md-outlined-text-field>
      <md-outlined-select label="Article filter" data-action="select" data-measure="selector">
        <md-select-option selected value="unread"><div slot="headline">Unread</div></md-select-option>
        <md-select-option value="all"><div slot="headline">All articles</div></md-select-option>
        <md-select-option value="saved"><div slot="headline">Saved</div></md-select-option>
      </md-outlined-select>
    </div>
    <div class="row wrap"><label class="row md-typescale-body-medium"><md-checkbox checked></md-checkbox>Keep articles offline</label>
      <label class="row md-typescale-body-medium"><md-switch selected data-measure="switch-selected"></md-switch>Show unread counts</label></div>
    <p class="md-typescale-body-small">This package supplies tabs and lists. It does not supply a production navigation drawer or card component.</p>
  </main>
</div>
<md-dialog data-measure="popup">
  <div slot="headline">Reading preferences</div>
  <form slot="content" id="preferences" method="dialog" class="stack">
    <label class="row md-typescale-body-medium"><md-switch selected></md-switch>Show unread counts</label>
    <p class="md-typescale-body-medium">Changes apply to this device.</p>
  </form>
  <div slot="actions"><md-text-button form="preferences">Done</md-text-button></div>
</md-dialog>`;
document.querySelector('[data-action="popup"]').addEventListener('click', () => document.querySelector('md-dialog').show());
window.baselineReady = true;
