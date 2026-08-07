import { Card, CardBody, CardHeader } from '@heroui/card';
import { Spinner } from '@heroui/spinner';
import { useEffect, useState } from 'react';

import { SessionInfo, tpgaApi } from '@/api/tpga';
import useI18n from '@/hooks/use-i18n';

//OverviewPage 概览 展示当前登录用户与 API 账户/玩家计数
export default function OverviewPage () {
  const { t } = useI18n();
  const [session, setSession] = useState<SessionInfo | null>(null);
  const [accountCount, setAccountCount] = useState(0);
  const [playerCount, setPlayerCount] = useState(0);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    Promise.allSettled([
      tpgaApi.getSession(),
      tpgaApi.listApiAccounts(),
      tpgaApi.listPlayers(),
    ]).then(([s, a, p]) => {
      if (s.status === 'fulfilled') setSession(s.value);
      if (a.status === 'fulfilled') setAccountCount(a.value.accounts.length);
      if (p.status === 'fulfilled') setPlayerCount(p.value.players.length);
      setLoading(false);
    });
  }, []);

  if (loading) {
    return <div className='flex justify-center px-10'><Spinner /></div>;
  }

  const stats = [
    { label: t('webui.overview.session_user'), value: session?.username ?? '-' },
    { label: t('webui.overview.api_accounts'), value: accountCount },
    { label: t('webui.overview.players'), value: playerCount },
  ];

  return (
    <div className='flex flex-col gap-4 p-4'>
      <Card>
        <CardHeader className='text-xl font-bold'>{t('webui.overview.welcome')}</CardHeader>
      </Card>
      <div className='grid grid-cols-1 md:grid-cols-3 gap-4'>
        {stats.map((s) => (
          <Card key={s.label}>
            <CardHeader className='text-small text-default-500'>{s.label}</CardHeader>
            <CardBody className='text-2xl font-bold'>{s.value}</CardBody>
          </Card>
        ))}
      </div>
    </div>
  );
}
