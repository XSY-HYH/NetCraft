import { Button } from '@heroui/button';
import { Card, CardBody, CardHeader } from '@heroui/card';
import { Input } from '@heroui/input';
import { Spinner } from '@heroui/spinner';
import { motion } from 'motion/react';
import { useEffect, useState } from 'react';
import { toast } from 'react-hot-toast';

import { PlayerError, playerApi } from '@/api/player';
import useI18n from '@/hooks/use-i18n';
import PlayerLayout from '@/layouts/player';

//PlayerProfilePage 档案管理 修改用户名/邮箱
//username 非空且不同 → 查重 → 更新 email null 不变 空串清空 非空查重
export default function PlayerProfilePage () {
  const { t, tError } = useI18n();
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [username, setUsername] = useState('');
  const [email, setEmail] = useState('');

  useEffect(() => {
    playerApi.getProfile()
      .then((p) => { setUsername(p.username ?? ''); setEmail(p.email ?? ''); })
      .catch(() => toast.error(t('webui.common.error')))
      .finally(() => setLoading(false));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const onSave = async () => {
    setSaving(true);
    try {
      await playerApi.updateProfile(username, email || null);
      toast.success(t('webui.common.save'));
    } catch (err) {
      toast.error(tError((err as PlayerError).error, t('webui.common.error')));
    } finally {
      setSaving(false);
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
            {t('webui.player.profile.title')}
          </CardHeader>
          <CardBody className='flex flex-col gap-4 py-4 px-6'>
            {loading ? (
              <div className='flex justify-center py-10'><Spinner /></div>
            ) : (
              <>
                <Input
                  label={t('webui.player.profile.username')}
                  value={username}
                  onChange={(e) => setUsername(e.target.value)}
                  description={t('webui.player.profile.username_desc')}
                />
                <Input
                  label={t('webui.player.profile.email_label')}
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  placeholder={t('webui.player.profile.email_placeholder')}
                  description={t('webui.player.profile.email_desc')}
                />
                <Button color='primary' isLoading={saving} onPress={onSave} className='mt-2'>
                  {t('webui.common.save')}
                </Button>
              </>
            )}
          </CardBody>
        </Card>
      </motion.div>
    </PlayerLayout>
  );
}
