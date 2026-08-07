import { Button } from '@heroui/button';
import { Card, CardBody, CardHeader } from '@heroui/card';
import { motion } from 'motion/react';
import { useNavigate } from 'react-router-dom';

import { title } from '@/components/primitives';
import useI18n from '@/hooks/use-i18n';
import PureLayout from '@/layouts/pure';

//ComingSoonPage 占位页 /auth/register /auth/forgot 等暂未实现的功能
//阶段5/6实现注册和邮箱验证后会替换为真实页面 避免PCL-CE跳转404
export default function ComingSoonPage () {
  const { t } = useI18n();
  const navigate = useNavigate();
  return (
    <PureLayout>
      <motion.div
        initial={{ opacity: 0, y: 20, scale: 0.95 }}
        animate={{ opacity: 1, y: 0, scale: 1 }}
        transition={{ duration: 0.5, type: 'spring', stiffness: 120, damping: 20 }}
        className='w-[480px] max-w-full py-8 px-4'
      >
        <Card className='border border-default-200 dark:border-default-100'>
          <CardHeader className='flex flex-col items-center gap-2 pt-8'>
            <span className={title({ color: 'violet' })}>{t('webui.player.coming_soon')}</span>
          </CardHeader>
          <CardBody className='flex flex-col gap-4 py-6 px-8 items-center'>
            <p className='text-default-500 text-center'>{t('webui.player.coming_soon_desc')}</p>
            <Button color='primary' variant='flat' onPress={() => navigate('/login', { replace: true })}>
              {t('webui.player.back_to_login')}
            </Button>
          </CardBody>
        </Card>
      </motion.div>
    </PureLayout>
  );
}
