import { Button } from '@heroui/button';
import { Card, CardBody, CardHeader } from '@heroui/card';
import { Input } from '@heroui/input';
import { Spinner } from '@heroui/spinner';
import { motion } from 'motion/react';
import { useEffect, useState } from 'react';
import { toast } from 'react-hot-toast';
import { IoArrowBackOutline } from 'react-icons/io5';
import { useNavigate, useSearchParams } from 'react-router-dom';

import { PendingOAuthInfo, PlayerError, playerApi } from '@/api/player';
import { title } from '@/components/primitives';
import usePlayerAuth from '@/hooks/player-auth';
import useI18n from '@/hooks/use-i18n';
import PureLayout from '@/layouts/pure';

//PlayerRegisterPage OAuth 补全注册页 OAuth 回调未绑定本地账户时跳此
//读 query token 取暂存身份预填 username/email 用户确认后建账户绑定 provider+subject
//token 无效或过期跳回登录 避免直接访问无 OAuth 身份
export default function PlayerRegisterPage () {
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const { t, tError } = useI18n();
  const { setAuth } = usePlayerAuth();
  const token = params.get('token') || '';
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [username, setUsername] = useState('');
  const [email, setEmail] = useState('');

  useEffect(() => {
    if (!token) { navigate('/login', { replace: true }); return; }
    playerApi.getPendingOAuth(token)
      .then((p: PendingOAuthInfo) => {
        setUsername(p.username ?? '');
        setEmail(p.email ?? '');
      })
      .catch((err) => {
        toast.error(tError((err as PlayerError).error, t('webui.register.token_invalid')));
        navigate('/login', { replace: true });
      })
      .finally(() => setLoading(false));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!username) { toast.error(t('webui.player.login.username_required')); return; }
    setSaving(true);
    try {
      await playerApi.register(token, username, email || null);
      setAuth(true);
      toast.success(t('webui.register.success'));
      navigate('/', { replace: true });
    } catch (err) {
      toast.error(tError((err as PlayerError).error, t('webui.register.failed')));
    } finally {
      setSaving(false);
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
            <span className={title({ color: 'violet' })}>{t('webui.register.title')}</span>
            <span className='text-small text-default-400'>{t('webui.register.desc')}</span>
          </CardHeader>
          <CardBody className='flex flex-col gap-4 py-6 px-8'>
            {loading ? (
              <div className='flex justify-center py-10'><Spinner /></div>
            ) : (
              <form onSubmit={onSubmit} className='flex flex-col gap-4'>
                <Input
                  type='text'
                  autoComplete='username'
                  isDisabled={saving}
                  label={t('webui.player.profile.username')}
                  description={t('webui.player.profile.username_desc')}
                  radius='lg'
                  size='lg'
                  value={username}
                  onChange={(e) => setUsername(e.target.value)}
                />
                <Input
                  type='email'
                  autoComplete='email'
                  isDisabled={saving}
                  label={t('webui.player.profile.email_label')}
                  placeholder={t('webui.player.profile.email_placeholder')}
                  description={t('webui.player.profile.email_desc')}
                  radius='lg'
                  size='lg'
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                />
                <div className='flex gap-3 mt-2'>
                  <Button
                    variant='flat'
                    isDisabled={saving}
                    onPress={() => navigate('/login')}
                    startContent={<IoArrowBackOutline size={20} />}
                  >
                    {t('webui.common.back')}
                  </Button>
                  <Button
                    type='submit'
                    color='primary'
                    isLoading={saving}
                    className='flex-1'
                    radius='full'
                    size='lg'
                  >
                    {t('webui.register.submit')}
                  </Button>
                </div>
              </form>
            )}
          </CardBody>
        </Card>
      </motion.div>
    </PureLayout>
  );
}
