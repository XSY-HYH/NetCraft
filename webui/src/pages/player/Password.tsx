import { Button } from '@heroui/button';
import { Card, CardBody, CardHeader } from '@heroui/card';
import { Input } from '@heroui/input';
import { motion } from 'motion/react';
import { useState } from 'react';
import { toast } from 'react-hot-toast';
import { IoArrowBackOutline } from 'react-icons/io5';
import { useNavigate } from 'react-router-dom';

import { PlayerError, playerApi } from '@/api/player';
import useI18n from '@/hooks/use-i18n';
import PlayerLayout from '@/layouts/player';

//PlayerPasswordPage 修改密码 旧密码校验 新密码至少 4 位 两次输入一致
//form submit 触发浏览器密码管理器更新 saved password
export default function PlayerPasswordPage () {
  const { t, tError } = useI18n();
  const navigate = useNavigate();
  const [oldPwd, setOldPwd] = useState('');
  const [newPwd, setNewPwd] = useState('');
  const [confirm, setConfirm] = useState('');
  const [loading, setLoading] = useState(false);

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newPwd) { toast.error(t('webui.password.new_required')); return; }
    if (newPwd !== confirm) { toast.error(t('webui.password.mismatch')); return; }
    setLoading(true);
    try {
      await playerApi.changePassword(oldPwd, newPwd);
      toast.success(t('webui.password.changed'));
      setOldPwd(''); setNewPwd(''); setConfirm('');
    } catch (err) {
      toast.error(tError((err as PlayerError).error, t('webui.password.failed')));
    } finally {
      setLoading(false);
    }
  };

  return (
    <PlayerLayout>
      <motion.div
        initial={{ opacity: 0, y: 20 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.4 }}
        className='max-w-xl mx-auto p-4 py-8'
      >
        <Card className='border border-default-200 dark:border-default-100'>
          <CardHeader className='text-xl font-bold pt-6 px-6'>
            {t('webui.password.title')}
          </CardHeader>
          <CardBody className='flex flex-col gap-4 py-4 px-6'>
            <form onSubmit={onSubmit} className='flex flex-col gap-4'>
              <Input
                type='password'
                autoComplete='current-password'
                isDisabled={loading}
                label={t('webui.password.old')}
                value={oldPwd}
                onChange={(e) => setOldPwd(e.target.value)}
              />
              <Input
                type='password'
                autoComplete='new-password'
                isDisabled={loading}
                label={t('webui.password.new')}
                value={newPwd}
                onChange={(e) => setNewPwd(e.target.value)}
              />
              <Input
                type='password'
                autoComplete='new-password'
                isDisabled={loading}
                label={t('webui.password.confirm')}
                value={confirm}
                onChange={(e) => setConfirm(e.target.value)}
              />
              <div className='flex gap-3 mt-2'>
                <Button
                  variant='flat'
                  isDisabled={loading}
                  onPress={() => navigate(-1)}
                  startContent={<IoArrowBackOutline size={20} />}
                >
                  {t('webui.common.back')}
                </Button>
                <Button
                  type='submit'
                  color='primary'
                  isLoading={loading}
                  className='flex-1'
                >
                  {t('webui.password.submit')}
                </Button>
              </div>
            </form>
          </CardBody>
        </Card>
      </motion.div>
    </PlayerLayout>
  );
}
