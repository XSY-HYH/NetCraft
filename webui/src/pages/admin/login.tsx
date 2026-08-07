import { Button } from '@heroui/button';
import { Card, CardBody, CardHeader } from '@heroui/card';
import { Input } from '@heroui/input';
import { motion } from 'motion/react';
import { useState } from 'react';
import { toast } from 'react-hot-toast';
import { IoKeyOutline, IoPersonOutline } from 'react-icons/io5';
import { useNavigate } from 'react-router-dom';

import { TpgaError, tpgaApi } from '@/api/tpga';
import { title } from '@/components/primitives';
import useAuth from '@/hooks/auth';
import useI18n from '@/hooks/use-i18n';

import PureLayout from '@/layouts/pure';

//AdminLoginPage 管理员登录 form submit 触发浏览器密码管理器保存凭证
export default function AdminLoginPage () {
  const navigate = useNavigate();
  const { t, tError } = useI18n();
  const { setAuth } = useAuth();
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [loading, setLoading] = useState(false);

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!username) { toast.error(t('webui.login.username_required')); return; }
    if (!password) { toast.error(t('webui.login.password_required')); return; }
    setLoading(true);
    try {
      const res = await tpgaApi.login(username, password);
      setAuth(true);
      if (res.mustChangePassword) {
        toast.success(t('webui.login.must_change'));
        navigate('/password', { replace: true });
      } else {
        navigate('/', { replace: true });
      }
    } catch (err) {
      toast.error(tError((err as TpgaError).error, t('webui.login.failed')));
    } finally {
      setLoading(false);
    }
  };

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
            <span className={title()}>NetCraft.TPGA&nbsp;</span>
            <span className={title({ color: 'violet' })}>{t('webui.login.title')}</span>
          </CardHeader>
          <CardBody className='flex flex-col gap-4 py-6 px-8'>
            <form onSubmit={onSubmit} className='flex flex-col gap-4'>
              <Input
                type='text'
                autoComplete='username'
                isDisabled={loading}
                label={t('webui.login.username')}
                placeholder={t('webui.login.username_placeholder')}
                radius='lg'
                size='lg'
                startContent={<IoPersonOutline className='text-default-400' />}
                value={username}
                onChange={(e) => setUsername(e.target.value)}
              />
              <Input
                type='password'
                autoComplete='current-password'
                isDisabled={loading}
                label={t('webui.login.password')}
                placeholder={t('webui.login.password_placeholder')}
                radius='lg'
                size='lg'
                startContent={<IoKeyOutline className='text-default-400' />}
                value={password}
                onChange={(e) => setPassword(e.target.value)}
              />
              <Button
                className='mt-4 text-lg py-7'
                color='primary'
                isLoading={loading}
                radius='full'
                size='lg'
                type='submit'
                variant='shadow'
              >
                {t('webui.login.submit')}
              </Button>
            </form>
          </CardBody>
        </Card>
      </motion.div>
    </PureLayout>
  );
}
