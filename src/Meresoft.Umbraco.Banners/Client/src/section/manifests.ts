export const SECTION_ALIAS = "Meresoft.Banners.Section";
export const MENU_ALIAS = "Meresoft.Banners.Menu";

const menuItems = [
  { key: "Banners", label: "#meresoftBanners_banners", icon: "icon-picture", path: "banners" },
  { key: "Zones", label: "#meresoftBanners_zones", icon: "icon-grid", path: "zones" },
  { key: "Reports", label: "#meresoftBanners_reports", icon: "icon-chart", path: "reports" },
];

export const manifests: Array<UmbExtensionManifest> = [
  {
    type: "section",
    alias: SECTION_ALIAS,
    name: "Meresoft Banners Section",
    weight: 50,
    meta: {
      label: "#meresoftBanners_section",
      pathname: "meresoft-banners",
    },
    conditions: [
      {
        alias: "Umb.Condition.SectionUserPermission",
        match: SECTION_ALIAS,
      },
    ],
  },
  {
    type: "menu",
    alias: MENU_ALIAS,
    name: "Meresoft Banners Menu",
  },
  {
    type: "sectionSidebarApp",
    kind: "menu",
    alias: "Meresoft.Banners.SidebarApp",
    name: "Meresoft Banners Sidebar App",
    weight: 100,
    meta: {
      label: "#meresoftBanners_section",
      menu: MENU_ALIAS,
    },
    conditions: [
      {
        alias: "Umb.Condition.SectionAlias",
        match: SECTION_ALIAS,
      },
    ],
  },
  ...menuItems.map(
    (item, index): UmbExtensionManifest => ({
      type: "menuItem",
      kind: "link",
      alias: `Meresoft.Banners.MenuItem.${item.key}`,
      name: `Meresoft Banners ${item.key} Menu Item`,
      weight: 300 - index * 100,
      meta: {
        label: item.label,
        icon: item.icon,
        href: `section/meresoft-banners/${item.path}`,
        menus: [MENU_ALIAS],
      },
    }),
  ),
];
