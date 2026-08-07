import { Button } from '@heroui/button';
import { Card, CardBody, CardHeader } from '@heroui/card';
import { Chip } from '@heroui/chip';
import { Spinner } from '@heroui/spinner';
import { motion } from 'motion/react';
import { Suspense, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { IoKeyOutline, IoPersonOutline, IoShirtOutline } from 'react-icons/io5';

import { PlayerProfile, playerApi } from '@/api/player';
import SkinPreview3D from '@/components/skin_preview_3d';
import useI18n from '@/hooks/use-i18n';
import PlayerLayout from '@/layouts/player';

//PlayerHomePage 个人中心首页 信息卡组布局
//账户信息卡(用户名/UUID/邮箱+验证) 账户安全卡(登录方式/注册时间) 皮肤预览卡 快捷操作入口
export default function PlayerHomePage () {
  const { t } = useI18n();
  const navigate = useNavigate();
  const [profile, setProfile] = useState<PlayerProfile | null>(null);

  useEffect(() => {
    playerApi.getProfile().then(setProfile).catch(() => {});
  }, []);

  return (
    <PlayerLayout>
      <motion.div
        initial={{ opacity: 0, y: 20 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.4 }}
        className='max-w-3xl mx-auto p-4 py-8'
      >
        <div className='text-2xl font-bold mb-6'>
          {t('webui.player.home.welcome')}{profile?.username ? `, ${profile.username}` : ''}
        </div>
        {!profile ? (
          <Card className='border border-default-200 dark:border-default-100'>
            <CardBody className='flex justify-center py-10'><Spinner /></CardBody>
          </Card>
        ) : (
          <div className='grid grid-cols-1 md:grid-cols-2 gap-4'>
            {/*账户信息卡 用户名/UUID/邮箱+验证状态*/}
            <Card className='border border-default-200 dark:border-default-100'>
              <CardHeader className='text-lg font-bold pt-5 px-6'>{t('webui.player.home.account_info')}</CardHeader>
              <CardBody className='flex flex-col py-4 px-6'>
                <Row label={t('webui.player.profile.username')} value={profile.username ?? ''} />
                <Row label='UUID' value={profile.uuid ?? ''} mono />
                <div className='flex justify-between items-center gap-2 py-3'>
                  <span className='text-default-500 shrink-0'>{t('webui.player.profile.email_label')}</span>
                  <div className='flex items-center gap-2 min-w-0 justify-end'>
                    <span className='truncate'>{profile.email || '-'}</span>
                    {profile.email && (
                      <Chip color={profile.emailVerified ? 'success' : 'warning'} variant='flat' size='sm'>
                        {profile.emailVerified ? t('webui.common.verified') : t('webui.common.unverified')}
                      </Chip>
                    )}
                  </div>
                </div>
              </CardBody>
            </Card>

            {/*账户安全卡 登录方式与注册时间 OAuth 账户展示提供商名*/}
            <Card className='border border-default-200 dark:border-default-100'>
              <CardHeader className='text-lg font-bold pt-5 px-6'>{t('webui.player.home.account_security')}</CardHeader>
              <CardBody className='flex flex-col py-4 px-6'>
                <Row label={t('webui.player.home.login_method')} value={getLoginMethod(profile, t)} />
                <Row label={t('webui.player.home.register_time')} value={formatTime(profile.createdAt)} last />
              </CardBody>
            </Card>

            {/*皮肤预览卡 有 skinHash 渲染 3D 模型 无则占位提示*/}
            <Card className='border border-default-200 dark:border-default-100 md:col-span-2'>
              <CardHeader className='text-lg font-bold pt-5 px-6'>{t('webui.player.home.skin')}</CardHeader>
              <CardBody className='py-4 px-6'>
                {profile.skinHash ? (
                  <div className='flex flex-col items-center gap-3'>
                    <Suspense fallback={<Spinner />}>
                      <SkinPreview3D
                        skinUrl={`/textures/${profile.skinHash}`}
                        model={profile.skinModel === 'slim' ? 'slim' : 'default'}
                        width={200}
                        height={300}
                      />
                    </Suspense>
                    <Button size='sm' variant='flat' color='primary' onPress={() => navigate('/user/closet')}>
                      {t('webui.player.home.manage_skin')}
                    </Button>
                  </div>
                ) : (
                  <div className='flex flex-col items-center justify-center gap-3 py-10 text-default-400'>
                    <IoShirtOutline className='text-5xl' />
                    <span className='text-small'>{t('webui.player.skin.no_skin')}</span>
                    <Button size='sm' variant='flat' color='primary' onPress={() => navigate('/user/closet')}>
                      {t('webui.player.home.manage_skin')}
                    </Button>
                  </div>
                )}
              </CardBody>
            </Card>

            {/*快捷操作卡 改档案/改密码/管理皮肤 跨两列*/}
            <Card className='border border-default-200 dark:border-default-100 md:col-span-2'>
              <CardHeader className='text-lg font-bold pt-5 px-6'>{t('webui.player.home.quick_actions')}</CardHeader>
              <CardBody className='py-4 px-6'>
                <div className='grid grid-cols-1 sm:grid-cols-3 gap-3'>
                  <Button variant='flat' startContent={<IoPersonOutline size={20} />} onPress={() => navigate('/profile')}>
                    {t('webui.player.home.action_profile')}
                  </Button>
                  <Button variant='flat' startContent={<IoKeyOutline size={20} />} onPress={() => navigate('/password')}>
                    {t('webui.player.home.action_password')}
                  </Button>
                  <Button variant='flat' startContent={<IoShirtOutline size={20} />} onPress={() => navigate('/user/closet')}>
                    {t('webui.player.home.action_closet')}
                  </Button>
                </div>
              </CardBody>
            </Card>
          </div>
        )}
      </motion.div>
    </PlayerLayout>
  );
}

//getLoginMethod 登录方式 oauthProvider 空=密码账户 非空=OAuth 账户显示提供商名
function getLoginMethod (profile: PlayerProfile, t: (k: string) => string): string {
  const p = profile.oauthProvider;
  if (!p) return t('webui.player.home.method_password');
  const name = p.charAt(0).toUpperCase() + p.slice(1);
  return t('webui.player.home.method_oauth').replace('{provider}', name);
}

//formatTime 注册时间 ISO 字符串转本地时间 解析失败原样返回
function formatTime (s?: string): string {
  if (!s) return '-';
  const d = new Date(s);
  if (isNaN(d.getTime())) return s;
  return d.toLocaleString();
}

//Row 标签值行 mono 等宽显示 UUID last 控制底边框
function Row ({ label, value, mono, last }: { label: string; value: string; mono?: boolean; last?: boolean }) {
  return (
    <div className={`flex justify-between items-center gap-2 py-3 ${last ? '' : 'border-b border-default-100'}`}>
      <span className='text-default-500 shrink-0'>{label}</span>
      <span className={mono ? 'font-mono text-small break-all text-right' : 'text-right break-all'}>{value}</span>
    </div>
  );
}
