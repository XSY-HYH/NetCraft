import {
  LuInfo,
  LuKey,
  LuKeyRound,
  LuLayoutDashboard,
  LuUsers,
} from 'react-icons/lu';

export type SiteConfig = typeof siteConfig;
export interface MenuItem {
  label: string;
  icon?: React.ReactNode;
  autoOpen?: boolean;
  href?: string;
  items?: MenuItem[];
  customIcon?: string;
}

//导航 label 到 i18n key 映射 label 仍用中文作 customIcons 查找键
export const navI18nKeys: Record<string, string> = {
  '概览': 'webui.sidebar.nav.overview',
  'API账户': 'webui.sidebar.nav.api_accounts',
  '游戏玩家': 'webui.sidebar.nav.players',
  '修改密码': 'webui.sidebar.nav.password',
  '关于': 'webui.sidebar.nav.about',
};

//TPGA 管理后台侧边栏导航 概览/API账户/游戏玩家/修改密码/关于
export const siteConfig = {
  name: 'NetCraft.TPGA',
  description: 'NetCraft.TPGA Admin',
  navItems: [
    {
      label: '概览',
      icon: <LuLayoutDashboard className='w-5 h-5' />,
      href: '/',
    },
    {
      label: 'API账户',
      icon: <LuKeyRound className='w-5 h-5' />,
      href: '/api-accounts',
    },
    {
      label: '游戏玩家',
      icon: <LuUsers className='w-5 h-5' />,
      href: '/players',
    },
    {
      label: '修改密码',
      icon: <LuKey className='w-5 h-5' />,
      href: '/password',
    },
    {
      label: '关于',
      icon: <LuInfo className='w-5 h-5' />,
      href: '/about',
    },
  ] as MenuItem[],
  links: {
    github: 'https://github.com/XSY-xiaoqi/MorningCat',
  },
};
