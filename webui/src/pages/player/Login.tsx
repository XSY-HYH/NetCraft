import { Button } from '@heroui/button';
import { Card, CardBody, CardHeader } from '@heroui/card';
import { Input } from '@heroui/input';
import { motion } from 'motion/react';
import { useEffect, useState } from 'react';
import { toast } from 'react-hot-toast';
import { IoKeyOutline, IoPersonOutline } from 'react-icons/io5';
import { useNavigate, useSearchParams } from 'react-router-dom';

import { OAuthProvider, PlayerError, playerApi } from '@/api/player';
import { title } from '@/components/primitives';
import usePlayerAuth from '@/hooks/player-auth';
import useI18n from '@/hooks/use-i18n';
import PureLayout from '@/layouts/pure';

//PlayerLoginPage 玩家登录页 form submit 触发浏览器密码管理器保存凭证
//支持用户名或邮箱登录 后端 FindByLogin 兼容两种 PCL-CE 邮箱框填值
//OAuth 按钮区配置驱动 后端配几个前端显示几个 留空则只密码登录
export default function PlayerLoginPage () {
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const { t, tError } = useI18n();
  const { setAuth } = usePlayerAuth();
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [loading, setLoading] = useState(false);
  const [providers, setProviders] = useState<OAuthProvider[]>([]);

  //加载 OAuth 提供商列表 后端配置驱动 未配置返回空数组
  useEffect(() => {
    playerApi.getOAuthProviders()
      .then((r) => setProviders(r.providers ?? []))
      .catch(() => { /* OAuth 未配置或不可用 忽略 */ });
  }, []);

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!username) { toast.error(t('webui.player.login.username_required')); return; }
    if (!password) { toast.error(t('webui.player.login.password_required')); return; }
    setLoading(true);
    try {
      await playerApi.login(username, password);
      setAuth(true);
      const next = params.get('return') || '/';
      navigate(next, { replace: true });
    } catch (err) {
      toast.error(tError((err as PlayerError).error, t('webui.player.login.failed')));
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
            <span className={title()}>NetCraft&nbsp;</span>
            <span className={title({ color: 'violet' })}>{t('webui.player.login.title')}</span>
          </CardHeader>
          <CardBody className='flex flex-col gap-4 py-6 px-8'>
            <form onSubmit={onSubmit} className='flex flex-col gap-4'>
              <Input
                type='text'
                autoComplete='username'
                isDisabled={loading}
                label={t('webui.player.login.username')}
                placeholder={t('webui.player.login.username_placeholder')}
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
                label={t('webui.player.login.password')}
                placeholder={t('webui.player.login.password_placeholder')}
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
                {t('webui.player.login.submit')}
              </Button>
            </form>
            {providers.length > 0 && (
              <div className='flex flex-col gap-2 mt-2'>
                <div className='flex items-center gap-3 my-1'>
                  <div className='flex-1 h-px bg-default-200 dark:bg-default-100' />
                  <span className='text-small text-default-400'>{t('webui.player.login.or_oauth')}</span>
                  <div className='flex-1 h-px bg-default-200 dark:bg-default-100' />
                </div>
                {providers.map((p) => (
                  <Button
                    key={p.key}
                    variant='bordered'
                    radius='full'
                    size='lg'
                    className='py-6'
                    onPress={() => playerApi.oauthLogin(p.key)}
                  >
                    {t('webui.player.login.oauth_btn').replace('{name}', p.name)}
                  </Button>
                ))}
              </div>
            )}
          </CardBody>
        </Card>
      </motion.div>
    </PureLayout>
  );
}
