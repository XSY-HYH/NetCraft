import { Card, CardBody, CardHeader } from '@heroui/card';

import useI18n from '@/hooks/use-i18n';

//AboutPage 关于 展示 TPGA 版本与端口说明
export default function AboutPage () {
  const { t } = useI18n();

  return (
    <div className='flex flex-col gap-4 p-4'>
      <Card>
        <CardHeader className='text-xl font-bold'>{t('webui.about.title')}</CardHeader>
        <CardBody className='flex flex-col gap-3'>
          <div className='text-lg font-semibold'>NetCraft.TPGA</div>
          <div className='text-default-500'>{t('webui.about.version')}: v0.1.0</div>
          <div className='text-default-500'>{t('webui.about.desc')}</div>
          <div className='text-default-500'>{t('webui.about.main_port')}</div>
          <div className='text-default-500'>{t('webui.about.api_port')}</div>
        </CardBody>
      </Card>
    </div>
  );
}
