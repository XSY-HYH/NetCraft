import { Card, CardBody, CardHeader } from '@heroui/card';
import { motion } from 'motion/react';

import useI18n from '@/hooks/use-i18n';

import PureLayout from '@/layouts/pure';

//PlayerInfoPage 玩家端公共信息 展示 TPGA 服务说明与启动器配置入口
export default function PlayerInfoPage () {
  const { t } = useI18n();

  return (
    <PureLayout>
      <motion.div
        initial={{ opacity: 0, y: 20, scale: 0.95 }}
        animate={{ opacity: 1, y: 0, scale: 1 }}
        transition={{ duration: 0.5, type: 'spring', stiffness: 120, damping: 20 }}
        className='w-[640px] max-w-full m-4'
      >
        <Card className='border border-default-200 dark:border-default-100'>
          <CardHeader className='flex flex-col items-center gap-2 pt-8'>
            <span className='text-2xl font-bold'>{t('webui.app.player')}</span>
          </CardHeader>
          <CardBody className='flex flex-col gap-3 py-6 px-8'>
            <div className='text-default-500'>{t('webui.about.desc')}</div>
            <div className='text-default-500'>{t('webui.about.main_port')}</div>
            <div className='text-default-500'>{t('webui.about.api_port')}</div>
            <div className='mt-2 text-small text-default-400'>
              Yggdrasil API: https://&lt;server&gt;:25565
            </div>
            <div className='text-small text-default-400'>
              API / WSS: https://&lt;server&gt;:25566
            </div>
          </CardBody>
        </Card>
      </motion.div>
    </PureLayout>
  );
}
