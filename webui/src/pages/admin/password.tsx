import { Button } from '@heroui/button';
import { Card, CardBody, CardHeader } from '@heroui/card';
import { Input } from '@heroui/input';
import { motion } from 'motion/react';
import { useEffect, useState } from 'react';
import { toast } from 'react-hot-toast';
import { IoArrowBackOutline } from 'react-icons/io5';
import { useNavigate } from 'react-router-dom';

import { TpgaError, tpgaApi } from '@/api/tpga';
import { title } from '@/components/primitives';
import useAuth from '@/hooks/auth';
import useI18n from '@/hooks/use-i18n';

import PureLayout from '@/layouts/pure';

//AdminPasswordPage 修改管理员密码 首登强制改密与侧边栏改密共用
//form submit 触发浏览器密码管理器更新 saved password new-password 字段关联 username
//mustChangePassword=true 为首登强制改密 不显示返回键 false 为侧边栏主动改密 显示返回
export default function AdminPasswordPage () {
  const navigate = useNavigate();
  const { t, tError } = useI18n();
  const { setAuth } = useAuth();
  const [oldPwd, setOldPwd] = useState('');
  const [newPwd, setNewPwd] = useState('');
  const [confirm, setConfirm] = useState('');
  const [loading, setLoading] = useState(false);
  //mustChange=true 首登强制改密 无返回键 false 侧边栏改密 显示返回键
  const [mustChange, setMustChange] = useState(true);
  //hidden username 让浏览器密码管理器关联更新哪个账户
  const [username, setUsername] = useState('');

  useEffect(() => {
    tpgaApi.getSession()
      .then((s) => {
        if (s.ok) {
          setMustChange(!!s.mustChangePassword);
          if (s.username) setUsername(s.username);
        }
      })
      .catch(() => { /* 未登录或首登 session 未建立 忽略 */ });
  }, []);

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newPwd) { toast.error(t('webui.password.new_required')); return; }
    if (newPwd !== confirm) { toast.error(t('webui.password.mismatch')); return; }
    setLoading(true);
    try {
      await tpgaApi.changePassword(oldPwd, newPwd);
      setAuth(true);
      toast.success(t('webui.password.changed'));
      navigate('/', { replace: true });
    } catch (err) {
      toast.error(tError((err as TpgaError).error, t('webui.password.failed')));
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
            <span className={title({ color: 'violet' })}>{t('webui.password.title')}</span>
          </CardHeader>
          <CardBody className='flex flex-col gap-4 py-6 px-8'>
            <form onSubmit={onSubmit} className='flex flex-col gap-4'>
              {/*隐藏 username 让浏览器密码管理器知道更新哪个账户的密码*/}
              {username && (
                <input type='text' name='username' autoComplete='username' value={username} readOnly hidden />
              )}
              <Input
                type='password'
                autoComplete='current-password'
                isDisabled={loading}
                label={t('webui.password.old')}
                radius='lg'
                size='lg'
                value={oldPwd}
                onChange={(e) => setOldPwd(e.target.value)}
              />
              <Input
                type='password'
                autoComplete='new-password'
                isDisabled={loading}
                label={t('webui.password.new')}
                radius='lg'
                size='lg'
                value={newPwd}
                onChange={(e) => setNewPwd(e.target.value)}
              />
              <Input
                type='password'
                autoComplete='new-password'
                isDisabled={loading}
                label={t('webui.password.confirm')}
                radius='lg'
                size='lg'
                value={confirm}
                onChange={(e) => setConfirm(e.target.value)}
              />
              <div className='flex gap-3 mt-4'>
                {!mustChange && (
                  <Button
                    className='text-lg py-7'
                    isDisabled={loading}
                    radius='full'
                    size='lg'
                    variant='flat'
                    onPress={() => navigate(-1)}
                    startContent={<IoArrowBackOutline size={20} />}
                  >
                    {t('webui.common.back')}
                  </Button>
                )}
                <Button
                  className='flex-1 text-lg py-7'
                  color='primary'
                  isLoading={loading}
                  radius='full'
                  size='lg'
                  type='submit'
                  variant='shadow'
                >
                  {t('webui.password.submit')}
                </Button>
              </div>
            </form>
          </CardBody>
        </Card>
      </motion.div>
    </PureLayout>
  );
}
