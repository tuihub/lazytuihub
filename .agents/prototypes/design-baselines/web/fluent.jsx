import React from 'react';
import { createRoot } from 'react-dom/client';
import {
  FluentProvider, webLightTheme, webDarkTheme, Title1, Subtitle1, Body1, Caption1,
  Button, Dropdown, Option, Field, Input, Checkbox, Switch, Card, CardHeader,
  TabList, Tab, Popover, PopoverTrigger, PopoverSurface, Divider,
  Nav, NavItem, NavSectionHeader, tokens,
} from '@fluentui/react-components';
import { ArrowClockwise20Regular, Add20Regular, News20Regular, Settings20Regular } from '@fluentui/react-icons';

const dark = new URLSearchParams(location.search).get('theme') === 'dark';
const theme = dark ? webDarkTheme : webLightTheme;
window.baselineTokens = theme;
window.baselineScope = 'Microsoft Fluent UI React v9 / Fluent 2 Web; not WinUI';

function App() {
  return <FluentProvider theme={theme} style={{ minHeight: '100vh' }}>
    <div className="specimen" style={{ background: tokens.colorNeutralBackground1, color: tokens.colorNeutralForeground1 }}>
      <header className="heading">
        <Caption1>OFFICIAL COMPONENT BASELINE · FLUENT 2 WEB</Caption1>
        <Title1 as="h1" data-measure="title">A quieter place to read</Title1>
        <Body1 as="p" data-measure="body">A reader brings subscriptions, articles, and reading preferences together.</Body1>
        <Caption1 data-measure="caption">Fluent UI React v9 · default medium controls · {dark ? 'Dark' : 'Light'}</Caption1>
      </header>
      <div className="reader">
        <aside>
          <Nav aria-label="Reader navigation" defaultSelectedValue="articles" data-measure="navigation">
            <NavSectionHeader>Library</NavSectionHeader>
            <NavItem icon={<News20Regular />} value="articles" data-measure="nav-selected">Articles</NavItem>
            <NavItem icon={<Settings20Regular />} value="settings">Preferences</NavItem>
          </Nav>
        </aside>
        <main className="stack">
          <div className="row spread">
            <Subtitle1 as="h2" data-measure="section-title">Latest articles</Subtitle1>
            <Button icon={<ArrowClockwise20Regular />} aria-label="Refresh feeds" data-measure="icon-button" />
          </div>
          <TabList defaultSelectedValue="unread" aria-label="Article filter" data-measure="tabs">
            <Tab value="unread">Unread</Tab><Tab value="all">All articles</Tab><Tab value="saved">Saved</Tab>
          </TabList>
          <Card appearance="filled" selected onSelectionChange={() => {}} data-measure="card-selected">
            <CardHeader header={<Body1><b>The small details of a good reading experience</b></Body1>}
              description={<Caption1>Design journal · 8 minutes ago</Caption1>} />
            <Body1 data-measure="article-body">Clear typography and calm surfaces keep attention on the story. This paragraph uses the unmodified Body1 preset.</Body1>
          </Card>
          <Card appearance="outline" data-measure="card">
            <CardHeader header={<Body1>Building tools that leave room to think</Body1>}
              description={<Caption1>Engineering notes · Yesterday</Caption1>} />
          </Card>
          <Divider />
          <Subtitle1 as="h2">Controls and states</Subtitle1>
          <div className="row wrap">
            <Button appearance="primary" icon={<Add20Regular />} data-action="primary" data-measure="primary">Subscribe</Button>
            <Button appearance="secondary" icon={<Add20Regular />} data-measure="add-game">Add game</Button>
            <Button data-measure="secondary">Mark as read</Button>
            <Button disabled data-measure="disabled">Unavailable</Button>
            <Popover positioning="below-end">
              <PopoverTrigger disableButtonEnhancement><Button data-action="popup">Appearance</Button></PopoverTrigger>
              <PopoverSurface className="popup stack" data-measure="popup">
                <Subtitle1>Reading preferences</Subtitle1>
                <Switch defaultChecked label="Show unread counts" />
                <Body1>Changes apply to this device.</Body1>
              </PopoverSurface>
            </Popover>
          </div>
          <div className="row wrap align-start">
            <Field label="Subscription name"><Input defaultValue="Design journal" data-action="input" data-measure="input" /></Field>
            <Field label="Article filter"><Dropdown defaultValue="Unread" defaultSelectedOptions={['unread']} data-action="select" data-measure="selector">
              <Option value="unread">Unread</Option><Option value="all">All articles</Option><Option value="saved">Saved</Option>
            </Dropdown></Field>
          </div>
          <div className="row wrap">
            <Checkbox defaultChecked label="Keep articles offline" data-measure="checkbox-selected" />
            <Switch defaultChecked label="Show unread counts" data-measure="switch-selected" />
          </div>
        </main>
      </div>
    </div>
  </FluentProvider>;
}
createRoot(document.getElementById('root')).render(<App />);
window.baselineReady = true;
