import { Button } from '@heroui/button';
import { Dropdown, DropdownItem, DropdownMenu, DropdownTrigger } from '@heroui/dropdown';
import { useLocalStorage } from '@uidotdev/usehooks';
import clsx from 'clsx';
import { AnimatePresence, motion } from 'motion/react';
import React, { useEffect, useState } from 'react';
import { IoMdLogOut } from 'react-icons/io';
import { LuLanguages, LuMoon, LuSun } from 'react-icons/lu';

import key from '@/const/key';
import { tpgaApi } from '@/api/tpga';
import useAuth from '@/hooks/auth';
import useDialog from '@/hooks/use-dialog';
import useI18n from '@/hooks/use-i18n';
import { useTheme } from '@/hooks/use-theme';
import type { MenuItem } from '@/config/site';

import Menus from './menus';

interface SideBarProps {
  open: boolean;
  items: MenuItem[];
  onClose?: () => void;
}

//SideBar TPGA 管理后台侧边栏 logo+导航+个性化区(语言+主题)+登出
//语言选择从 /i18n/languages 拉列表 切换后 switchLanguage 持久化到管理员字段并重载
const SideBar: React.FC<SideBarProps> = (props) => {
  const { open, items, onClose } = props;
  const { revokeAuth } = useAuth();
  const dialog = useDialog();
  const { t, lang, translations, switchLanguage } = useI18n();
  const { isDark, toggle } = useTheme();
  const [backgroundImage] = useLocalStorage<string>(key.backgroundImage, '');
  const hasBackground = !!backgroundImage;
  //可用语言列表 切换器渲染用 后续扩展语言只需加 json 文件
  const [languages, setLanguages] = useState<string[]>([]);

  useEffect(() => {
    tpgaApi.getLanguages()
      .then((r) => setLanguages(r.languages))
      .catch(() => { /* 拉取失败用当前 lang 兜底 */ });
  }, []);

  //langLabel 语言代码转本地化名称 无翻译回退 code 本身
  const langLabel = (code: string) => translations[`webui.lang.${code}`] || code;

  const onRevokeAuth = () => {
    dialog.confirm({
      title: t('webui.sidebar.logout.title'),
      content: t('webui.sidebar.logout.content'),
      onConfirm: revokeAuth,
    });
  };

  return (
    <>
      <AnimatePresence initial={false}>
        {open && (
          <motion.div
            className='fixed inset-y-0 left-64 right-0 bg-black/20 backdrop-blur-[1px] z-40 md:hidden'
            aria-hidden='true'
            onClick={onClose}
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0, transition: { duration: 0.15 } }}
            transition={{ duration: 0.2, delay: 0.15 }}
          />
        )}
      </AnimatePresence>
      <motion.div
        className={clsx(
          'overflow-hidden fixed top-0 left-0 h-full z-50 md:static md:shadow-none rounded-r-2xl md:rounded-none',
          hasBackground
            ? 'bg-transparent backdrop-blur-md'
            : 'bg-content1/70 backdrop-blur-xl backdrop-saturate-150 shadow-xl',
          'md:bg-transparent md:backdrop-blur-none md:backdrop-saturate-100 md:shadow-none'
        )}
        initial={{ width: 0 }}
        animate={{ width: open ? '16rem' : 0 }}
        transition={{
          type: open ? 'spring' : 'tween',
          stiffness: 150,
          damping: open ? 15 : 10,
        }}
        style={{ overflow: 'hidden' }}
      >
        <motion.div className='w-64 flex flex-col items-stretch h-full transition-transform duration-300 ease-in-out z-30 relative float-right p-4'>
          <div className='flex items-center justify-start gap-3 px-2 my-8 ml-2'>
            <div className='h-5 w-1 bg-primary rounded-full shadow-sm' />
            <div className={clsx(
              'text-xl font-bold tracking-wide select-none',
              hasBackground ? 'text-white' : 'text-default-900 dark:text-white'
            )}
            >
              NetCraft.TPGA
            </div>
          </div>
          <div className='overflow-y-auto flex flex-col flex-1 px-2'>
            <Menus items={items} />
            <div className='mt-auto mb-10 md:mb-0 space-y-2 px-2'>
              {/*个性化区 语言选择列表 支持后续扩展更多语言*/}
              <Dropdown placement='top-start'>
                <DropdownTrigger>
                  <Button
                    className='w-full bg-default-50/50 hover:bg-default-100/80 text-default-500 font-medium shadow-sm hover:shadow-md transition-all duration-300 backdrop-blur-sm'
                    radius='full'
                    variant='flat'
                    startContent={<LuLanguages size={18} />}
                  >
                    {langLabel(lang)}
                  </Button>
                </DropdownTrigger>
                <DropdownMenu
                  aria-label='language select'
                  selectionMode='single'
                  selectedKeys={lang ? [lang] : []}
                  onAction={(k) => switchLanguage(String(k))}
                >
                  {languages.map((l) => (
                    <DropdownItem key={l}>{langLabel(l)}</DropdownItem>
                  ))}
                </DropdownMenu>
              </Dropdown>
              <Button
                className='w-full bg-default-50/50 hover:bg-default-100/80 text-default-500 font-medium shadow-sm hover:shadow-md transition-all duration-300 backdrop-blur-sm'
                radius='full'
                variant='flat'
                onPress={toggle}
                startContent={isDark ? <LuSun size={18} /> : <LuMoon size={18} />}
              >
                {t('webui.theme.toggle')}
              </Button>
              <Button
                className='w-full bg-danger-50/50 hover:bg-danger-100/80 text-danger-500 font-medium shadow-sm hover:shadow-md transition-all duration-300 backdrop-blur-sm'
                radius='full'
                variant='flat'
                onPress={onRevokeAuth}
                startContent={<IoMdLogOut size={18} />}
              >
                {t('webui.sidebar.logout_btn')}
              </Button>
            </div>
          </div>
        </motion.div>
      </motion.div>
    </>
  );
};

export default SideBar;
